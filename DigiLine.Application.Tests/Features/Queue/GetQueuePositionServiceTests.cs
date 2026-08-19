using DigiLine.Application.Queue;
using DigiLine.Application.Sessions;
using DigiLine.Domain.Common;
using DigiLine.Domain.Entities;

namespace DigiLine.Application.Tests;

public class GetQueuePositionServiceTests
{
    [Fact]
    public async Task GetAsync_ForWaitingToken_CountsOnlyEarlierWaitingTokens()
    {
        var session = CreateSession();
        var first = new QueueToken(session.Id, 1, "First", null);
        var completed = new QueueToken(session.Id, 2, "Completed", null);
        completed.Call(DateTime.UtcNow.AddMinutes(-5));
        completed.Complete(DateTime.UtcNow);
        var target = new QueueToken(session.Id, 3, "Target", null);
        var late = CreateLateToken(session.Id, 0);
        var repository = new StubQueueTokenRepository(
            late, target, completed, first);
        var service = CreateService(session, repository);

        var result = await service.GetAsync(session.Id, target.Id);

        Assert.Equal(2, result.Position);
        Assert.Equal(1, result.PatientsAhead);
        Assert.Equal(QueueTokenStatus.Waiting, result.Status);
    }

    [Fact]
    public async Task GetAsync_ForLateToken_CountsAllWaitingAndEarlierLateTokens()
    {
        var session = CreateSession();
        var waitingOne = new QueueToken(session.Id, 4, "Waiting 1", null);
        var waitingTwo = new QueueToken(session.Id, 5, "Waiting 2", null);
        var earlierLate = CreateLateToken(session.Id, 1);
        var target = CreateLateToken(session.Id, 3);
        var laterLate = CreateLateToken(session.Id, 6);
        var repository = new StubQueueTokenRepository(
            target, waitingTwo, laterLate, earlierLate, waitingOne);
        var service = CreateService(session, repository);

        var result = await service.GetAsync(session.Id, target.Id);

        Assert.Equal(4, result.Position);
        Assert.Equal(3, result.PatientsAhead);
        Assert.Equal(QueueTokenStatus.Late, result.Status);
    }

    [Fact]
    public async Task GetAsync_WhenTokenIsInactive_RejectsPosition()
    {
        var session = CreateSession();
        var token = new QueueToken(session.Id, 1, "Cancelled", null);
        token.Cancel();
        var repository = new StubQueueTokenRepository(token);
        var service = CreateService(session, repository);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.GetAsync(session.Id, token.Id));

        Assert.Equal(
            "Queue position is only available for waiting or late tokens.",
            exception.Message);
        Assert.False(repository.QueueWasRead);
    }

    [Fact]
    public async Task GetAsync_WhenSessionDoesNotExist_RejectsBeforeTokenLookup()
    {
        var repository = new StubQueueTokenRepository();
        var service = CreateService(null, repository);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.GetAsync(Guid.NewGuid(), Guid.NewGuid()));

        Assert.Equal("Session was not found.", exception.Message);
        Assert.False(repository.TokenWasRead);
    }

    [Fact]
    public async Task GetAsync_WhenSessionIsClosed_RejectsPosition()
    {
        var session = CreateSession();
        session.OpenBooking();
        session.Start();
        session.Close();
        var repository = new StubQueueTokenRepository();
        var service = CreateService(session, repository);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.GetAsync(session.Id, Guid.NewGuid()));

        Assert.Equal(
            "Queue position is not available for a closed session.",
            exception.Message);
        Assert.False(repository.TokenWasRead);
    }

    [Fact]
    public async Task GetAsync_WhenTokenIsFromAnotherSession_ThrowsNotFound()
    {
        var session = CreateSession();
        var otherToken = new QueueToken(Guid.NewGuid(), 1, "Other", null);
        var repository = new StubQueueTokenRepository(otherToken);
        var service = CreateService(session, repository);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.GetAsync(session.Id, otherToken.Id));

        Assert.Equal("Queue token was not found.", exception.Message);
        Assert.False(repository.QueueWasRead);
    }

    [Fact]
    public async Task GetAsync_WhenSessionIsPaused_PreservesQueuePosition()
    {
        var session = CreateSession();
        session.OpenBooking();
        session.Start();
        session.Pause();
        var first = new QueueToken(session.Id, 1, "First", null);
        var target = new QueueToken(session.Id, 2, "Target", null);
        var repository = new StubQueueTokenRepository(first, target);
        var service = CreateService(session, repository);

        var result = await service.GetAsync(session.Id, target.Id);

        Assert.Equal(2, result.Position);
        Assert.Equal(1, result.PatientsAhead);
    }

    private static GetQueuePositionService CreateService(
        Session? session,
        StubQueueTokenRepository repository) =>
        new(new StubSessionRepository(session), repository);

    private static QueueToken CreateLateToken(Guid sessionId, int tokenNumber)
    {
        var token = new QueueToken(sessionId, tokenNumber, $"Late {tokenNumber}", null);
        token.Call(DateTime.UtcNow.AddMinutes(-10));
        token.Skip();
        token.MarkLate();
        return token;
    }

    private static Session CreateSession()
    {
        var now = DateTime.UtcNow;
        return new Session(
            Guid.NewGuid(), Guid.NewGuid(), "Position test",
            now.AddMinutes(-30), now.AddMinutes(30),
            now.AddHours(2), now.AddHours(1), 10);
    }

    private sealed class StubSessionRepository : ISessionRepository
    {
        private readonly Session? _session;
        public StubSessionRepository(Session? session) => _session = session;
        public Task<Session?> GetByIdAsync(Guid sessionId, CancellationToken cancellationToken = default) =>
            Task.FromResult(_session?.Id == sessionId ? _session : null);
        public Task AddAsync(Session session, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task SaveChangesAsync(CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }

    private sealed class StubQueueTokenRepository : IQueueTokenRepository
    {
        private readonly IReadOnlyList<QueueToken> _tokens;
        public StubQueueTokenRepository(params QueueToken[] tokens) => _tokens = tokens;
        public bool TokenWasRead { get; private set; }
        public bool QueueWasRead { get; private set; }

        public Task<QueueToken?> GetByIdAsync(Guid sessionId, Guid tokenId, CancellationToken cancellationToken = default)
        {
            TokenWasRead = true;
            return Task.FromResult(_tokens.SingleOrDefault(token =>
                token.SessionId == sessionId && token.Id == tokenId));
        }

        public Task<IReadOnlyList<QueueToken>> GetBySessionOrderedAsync(Guid sessionId, CancellationToken cancellationToken = default)
        {
            QueueWasRead = true;
            return Task.FromResult<IReadOnlyList<QueueToken>>(
                _tokens.Where(token => token.SessionId == sessionId).ToList());
        }

        public Task<QueueToken?> GetNextWaitingForUpdateAsync(Guid sessionId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<int?> GetMaximumTokenNumberAsync(Guid sessionId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<int?> AllocateNextTokenNumberAsync(Guid sessionId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task AddAsync(QueueToken queueToken, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task SaveChangesAsync(CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }
}

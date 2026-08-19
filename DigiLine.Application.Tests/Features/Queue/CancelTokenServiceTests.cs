using DigiLine.Application.Queue;
using DigiLine.Application.Sessions;
using DigiLine.Domain.Common;
using DigiLine.Domain.Entities;

namespace DigiLine.Application.Tests;

public class CancelTokenServiceTests
{
    [Fact]
    public async Task CancelAsync_CancelsWaitingTokenBeforeSessionStarts()
    {
        var session = CreateScheduledSession();
        var token = new QueueToken(session.Id, 4, "Jane Doe", null);
        var repository = new StubQueueTokenRepository(token);
        var service = CreateService(session, repository);

        var result = await service.CancelAsync(session.Id, token.Id);

        Assert.Equal(token.Id, result.Id);
        Assert.Equal(QueueTokenStatus.Cancelled, result.Status);
        Assert.Equal(1, repository.SaveCount);
    }

    [Fact]
    public async Task CancelAsync_WhenSessionDoesNotExist_ThrowsBeforeLookup()
    {
        var repository = new StubQueueTokenRepository();
        var service = CreateService(null, repository);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.CancelAsync(Guid.NewGuid(), Guid.NewGuid()));

        Assert.Equal("Session was not found.", exception.Message);
        Assert.False(repository.WasRead);
    }

    [Fact]
    public async Task CancelAsync_WhenSessionIsClosed_RejectsCancellation()
    {
        var session = CreateRunningSession();
        session.Close();
        var repository = new StubQueueTokenRepository();
        var service = CreateService(session, repository);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.CancelAsync(session.Id, Guid.NewGuid()));

        Assert.Equal(
            "Cannot cancel a token for a closed session.",
            exception.Message);
        Assert.False(repository.WasRead);
    }

    [Fact]
    public async Task CancelAsync_WhenTokenIsNotInSession_ThrowsNotFound()
    {
        var session = CreateScheduledSession();
        var otherToken = new QueueToken(Guid.NewGuid(), 1, "Other", null);
        var repository = new StubQueueTokenRepository(otherToken);
        var service = CreateService(session, repository);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.CancelAsync(session.Id, otherToken.Id));

        Assert.Equal("Queue token was not found.", exception.Message);
        Assert.Equal(0, repository.SaveCount);
    }

    [Fact]
    public async Task CancelAsync_WhenTokenWasCalled_RejectsTransition()
    {
        var session = CreateRunningSession();
        var token = new QueueToken(session.Id, 1, "Called", null);
        token.Call(DateTime.UtcNow);
        var repository = new StubQueueTokenRepository(token);
        var service = CreateService(session, repository);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.CancelAsync(session.Id, token.Id));

        Assert.Equal("Only a waiting token can be cancelled.", exception.Message);
        Assert.Equal(0, repository.SaveCount);
    }

    [Fact]
    public async Task CancelAsync_WhenSessionIsPaused_AllowsWaitingTokenCancellation()
    {
        var session = CreateRunningSession();
        session.Pause();
        var token = new QueueToken(session.Id, 1, "Waiting", null);
        var repository = new StubQueueTokenRepository(token);
        var service = CreateService(session, repository);

        var result = await service.CancelAsync(session.Id, token.Id);

        Assert.Equal(QueueTokenStatus.Cancelled, result.Status);
        Assert.Equal(1, repository.SaveCount);
    }

    private static CancelTokenService CreateService(
        Session? session,
        StubQueueTokenRepository repository) =>
        new(new StubSessionRepository(session), repository);

    private static Session CreateScheduledSession()
    {
        var now = DateTime.UtcNow;
        return new Session(
            Guid.NewGuid(), Guid.NewGuid(), "Cancel test",
            now.AddMinutes(-30), now.AddMinutes(-10),
            now.AddHours(2), now.AddHours(1), 10);
    }

    private static Session CreateRunningSession()
    {
        var session = CreateScheduledSession();
        session.OpenBooking();
        session.Start();
        return session;
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
        public bool WasRead { get; private set; }
        public int SaveCount { get; private set; }

        public Task<QueueToken?> GetByIdAsync(Guid sessionId, Guid tokenId, CancellationToken cancellationToken = default)
        {
            WasRead = true;
            return Task.FromResult(_tokens.SingleOrDefault(token =>
                token.SessionId == sessionId && token.Id == tokenId));
        }

        public Task SaveChangesAsync(CancellationToken cancellationToken = default)
        {
            SaveCount++;
            return Task.CompletedTask;
        }

        public Task<IReadOnlyList<QueueToken>> GetBySessionOrderedAsync(Guid sessionId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<QueueToken?> GetNextWaitingForUpdateAsync(Guid sessionId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<int?> GetMaximumTokenNumberAsync(Guid sessionId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<int?> AllocateNextTokenNumberAsync(Guid sessionId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task AddAsync(QueueToken queueToken, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }
}

using DigiLine.Application.Queue;
using DigiLine.Application.Sessions;
using DigiLine.Domain.Common;
using DigiLine.Domain.Entities;

namespace DigiLine.Application.Tests;

public class RecallTokenServiceTests
{
    [Fact]
    public async Task RecallAsync_RefreshesCalledAtAndPersistsToken()
    {
        var session = CreateRunningSession();
        var token = new QueueToken(session.Id, 4, "Jane Doe", null);
        var originalCalledAt = DateTime.UtcNow.AddMinutes(-1);
        token.Call(originalCalledAt);
        var repository = new StubQueueTokenRepository(token);
        var service = new RecallTokenService(
            new StubSessionRepository(session),
            repository);

        var result = await service.RecallAsync(session.Id, token.Id);

        Assert.Equal(token.Id, result.Id);
        Assert.Equal(QueueTokenStatus.Called, result.Status);
        Assert.NotNull(result.CalledAt);
        Assert.True(result.CalledAt > originalCalledAt);
        Assert.Equal(1, repository.SaveCount);
    }

    [Fact]
    public async Task RecallAsync_WhenSessionDoesNotExist_ThrowsBeforeTokenLookup()
    {
        var repository = new StubQueueTokenRepository();
        var service = new RecallTokenService(
            new StubSessionRepository(null),
            repository);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.RecallAsync(Guid.NewGuid(), Guid.NewGuid()));

        Assert.Equal("Session was not found.", exception.Message);
        Assert.False(repository.WasRead);
    }

    [Fact]
    public async Task RecallAsync_WhenSessionIsNotRunning_RejectsRecall()
    {
        var session = CreateScheduledSession();
        var repository = new StubQueueTokenRepository();
        var service = new RecallTokenService(
            new StubSessionRepository(session),
            repository);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.RecallAsync(session.Id, Guid.NewGuid()));

        Assert.Equal(
            "Recall is only available for a running session.",
            exception.Message);
        Assert.False(repository.WasRead);
    }

    [Fact]
    public async Task RecallAsync_WhenTokenIsNotInSession_ThrowsNotFound()
    {
        var session = CreateRunningSession();
        var otherToken = new QueueToken(Guid.NewGuid(), 1, "Other", null);
        var repository = new StubQueueTokenRepository(otherToken);
        var service = new RecallTokenService(
            new StubSessionRepository(session),
            repository);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.RecallAsync(session.Id, otherToken.Id));

        Assert.Equal("Queue token was not found.", exception.Message);
        Assert.Equal(0, repository.SaveCount);
    }

    [Fact]
    public async Task RecallAsync_WhenTokenIsWaiting_RejectsTransition()
    {
        var session = CreateRunningSession();
        var token = new QueueToken(session.Id, 1, "Waiting", null);
        var repository = new StubQueueTokenRepository(token);
        var service = new RecallTokenService(
            new StubSessionRepository(session),
            repository);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.RecallAsync(session.Id, token.Id));

        Assert.Equal("Only a called token can be recalled.", exception.Message);
        Assert.Equal(0, repository.SaveCount);
    }

    private static Session CreateScheduledSession()
    {
        var now = DateTime.UtcNow;
        return new Session(
            Guid.NewGuid(), Guid.NewGuid(), "Recall test",
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

        public Task<Session?> GetByIdAsync(
            Guid sessionId,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(_session?.Id == sessionId ? _session : null);

        public Task AddAsync(Session session, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task SaveChangesAsync(CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
    }

    private sealed class StubQueueTokenRepository : IQueueTokenRepository
    {
        private readonly IReadOnlyList<QueueToken> _tokens;

        public StubQueueTokenRepository(params QueueToken[] tokens) =>
            _tokens = tokens;

        public bool WasRead { get; private set; }
        public int SaveCount { get; private set; }

        public Task<QueueToken?> GetByIdAsync(
            Guid sessionId,
            Guid tokenId,
            CancellationToken cancellationToken = default)
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

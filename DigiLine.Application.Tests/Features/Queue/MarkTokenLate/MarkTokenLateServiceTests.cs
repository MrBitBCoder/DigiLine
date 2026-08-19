using DigiLine.Application.Queue;
using DigiLine.Application.Sessions;
using DigiLine.Domain.Common;
using DigiLine.Domain.Entities;

namespace DigiLine.Application.Tests;

public class MarkTokenLateServiceTests
{
    [Fact]
    public async Task MarkLateAsync_TransitionsSkippedTokenAndPersists()
    {
        var session = CreateRunningSession();
        var token = CreateSkippedToken(session.Id);
        var repository = new StubQueueTokenRepository(token);
        var service = CreateService(session, repository);

        var result = await service.MarkLateAsync(session.Id, token.Id);

        Assert.Equal(token.Id, result.Id);
        Assert.Equal(QueueTokenStatus.Late, result.Status);
        Assert.NotNull(result.CalledAt);
        Assert.Equal(1, repository.SaveCount);
    }

    [Fact]
    public async Task MarkLateAsync_WhenSessionDoesNotExist_ThrowsBeforeLookup()
    {
        var repository = new StubQueueTokenRepository();
        var service = CreateService(null, repository);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.MarkLateAsync(Guid.NewGuid(), Guid.NewGuid()));

        Assert.Equal("Session was not found.", exception.Message);
        Assert.False(repository.WasRead);
    }

    [Fact]
    public async Task MarkLateAsync_WhenSessionIsNotRunning_RejectsChange()
    {
        var session = CreateScheduledSession();
        var repository = new StubQueueTokenRepository();
        var service = CreateService(session, repository);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.MarkLateAsync(session.Id, Guid.NewGuid()));

        Assert.Equal(
            "Late arrival handling is only available for a running session.",
            exception.Message);
        Assert.False(repository.WasRead);
    }

    [Fact]
    public async Task MarkLateAsync_WhenTokenIsNotInSession_ThrowsNotFound()
    {
        var session = CreateRunningSession();
        var otherToken = CreateSkippedToken(Guid.NewGuid());
        var repository = new StubQueueTokenRepository(otherToken);
        var service = CreateService(session, repository);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.MarkLateAsync(session.Id, otherToken.Id));

        Assert.Equal("Queue token was not found.", exception.Message);
        Assert.Equal(0, repository.SaveCount);
    }

    [Fact]
    public async Task MarkLateAsync_WhenTokenWasNotSkipped_RejectsTransition()
    {
        var session = CreateRunningSession();
        var token = new QueueToken(session.Id, 1, "Waiting", null);
        var repository = new StubQueueTokenRepository(token);
        var service = CreateService(session, repository);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.MarkLateAsync(session.Id, token.Id));

        Assert.Equal("Only a skipped token can be marked late.", exception.Message);
        Assert.Equal(0, repository.SaveCount);
    }

    private static MarkTokenLateService CreateService(
        Session? session,
        StubQueueTokenRepository repository) =>
        new(new StubSessionRepository(session), repository);

    private static QueueToken CreateSkippedToken(Guid sessionId)
    {
        var token = new QueueToken(sessionId, 1, "Late patient", null);
        token.Call(DateTime.UtcNow.AddMinutes(-10));
        token.Skip();
        return token;
    }

    private static Session CreateScheduledSession()
    {
        var now = DateTime.UtcNow;
        return new Session(
            Guid.NewGuid(), Guid.NewGuid(), "Late patient test",
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

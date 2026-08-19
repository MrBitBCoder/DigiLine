using DigiLine.Application.Sessions;
using DigiLine.Domain.Common;
using DigiLine.Domain.Entities;

namespace DigiLine.Application.Tests;

public class PauseSessionServiceTests
{
    [Fact]
    public async Task PauseAsync_WhenRunning_PausesAndPersistsSession()
    {
        var session = CreateRunningSession();
        var repository = new StubSessionRepository(session);
        var service = new PauseSessionService(repository);

        var result = await service.PauseAsync(session.Id);

        Assert.Equal(session.Id, result.SessionId);
        Assert.Equal(SessionStatus.Paused, result.Status);
        Assert.Equal(SessionStatus.Paused, session.Status);
        Assert.Equal(1, repository.SaveCount);
    }

    [Fact]
    public async Task PauseAsync_WhenSessionDoesNotExist_ThrowsWithoutSaving()
    {
        var repository = new StubSessionRepository(null);
        var service = new PauseSessionService(repository);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.PauseAsync(Guid.NewGuid()));

        Assert.Equal("Session was not found.", exception.Message);
        Assert.Equal(0, repository.SaveCount);
    }

    [Fact]
    public async Task PauseAsync_WhenSessionIsNotRunning_RejectsTransition()
    {
        var session = CreateSession();
        var repository = new StubSessionRepository(session);
        var service = new PauseSessionService(repository);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.PauseAsync(session.Id));

        Assert.Equal("Only a running session can be paused.", exception.Message);
        Assert.Equal(0, repository.SaveCount);
    }

    private static Session CreateRunningSession()
    {
        var session = CreateSession();
        session.OpenBooking();
        session.Start();
        return session;
    }

    private static Session CreateSession()
    {
        var now = DateTime.UtcNow;
        return new Session(
            Guid.NewGuid(), Guid.NewGuid(), "Pause test",
            now.AddMinutes(-30), now.AddMinutes(-10),
            now.AddHours(2), now.AddHours(1), 10);
    }

    private sealed class StubSessionRepository : ISessionRepository
    {
        private readonly Session? _session;
        public StubSessionRepository(Session? session) => _session = session;
        public int SaveCount { get; private set; }

        public Task<Session?> GetByIdAsync(Guid sessionId, CancellationToken cancellationToken = default) =>
            Task.FromResult(_session?.Id == sessionId ? _session : null);
        public Task AddAsync(Session session, CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public Task SaveChangesAsync(CancellationToken cancellationToken = default)
        {
            SaveCount++;
            return Task.CompletedTask;
        }
    }
}

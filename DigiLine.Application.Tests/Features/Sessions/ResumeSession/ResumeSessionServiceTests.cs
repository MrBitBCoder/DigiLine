using DigiLine.Application.Sessions;
using DigiLine.Domain.Common;
using DigiLine.Domain.Entities;

namespace DigiLine.Application.Tests;

public class ResumeSessionServiceTests
{
    [Fact]
    public async Task ResumeAsync_WhenPaused_ResumesAndPersistsSession()
    {
        var session = CreatePausedSession();
        var repository = new StubSessionRepository(session);
        var service = new ResumeSessionService(repository);

        var result = await service.ResumeAsync(session.Id);

        Assert.Equal(session.Id, result.SessionId);
        Assert.Equal(SessionStatus.Running, result.Status);
        Assert.Equal(SessionStatus.Running, session.Status);
        Assert.Equal(1, repository.SaveCount);
    }

    [Fact]
    public async Task ResumeAsync_WhenSessionDoesNotExist_ThrowsWithoutSaving()
    {
        var repository = new StubSessionRepository(null);
        var service = new ResumeSessionService(repository);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.ResumeAsync(Guid.NewGuid()));

        Assert.Equal("Session was not found.", exception.Message);
        Assert.Equal(0, repository.SaveCount);
    }

    [Fact]
    public async Task ResumeAsync_WhenSessionIsNotPaused_RejectsTransition()
    {
        var session = CreateSession();
        var repository = new StubSessionRepository(session);
        var service = new ResumeSessionService(repository);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.ResumeAsync(session.Id));

        Assert.Equal("Only a paused session can be resumed.", exception.Message);
        Assert.Equal(0, repository.SaveCount);
    }

    private static Session CreatePausedSession()
    {
        var session = CreateSession();
        session.OpenBooking();
        session.Start();
        session.Pause();
        return session;
    }

    private static Session CreateSession()
    {
        var now = DateTime.UtcNow;
        return new Session(
            Guid.NewGuid(), Guid.NewGuid(), "Resume test",
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

using DigiLine.Application.Sessions;
using DigiLine.Domain.Common;
using DigiLine.Domain.Entities;

namespace DigiLine.Application.Tests;

public class SessionLifecycleServiceTests
{
    [Fact]
    public async Task StartAsync_WhenBookingIsOpen_StartsAndPersists()
    {
        var session = CreateSession(); session.OpenBooking();
        var repository = new StubRepository(session);

        var result = await new StartSessionService(repository).StartAsync(session.Id);

        Assert.Equal(SessionStatus.Running, result.Status);
        Assert.Equal(SessionStatus.Running, session.Status);
        Assert.Equal(1, repository.SaveCount);
    }

    [Fact]
    public async Task StartAsync_WhenScheduled_RejectsWithoutSaving()
    {
        var session = CreateSession(); var repository = new StubRepository(session);
        var error = await Assert.ThrowsAsync<InvalidOperationException>(() => new StartSessionService(repository).StartAsync(session.Id));
        Assert.Equal("A session can only be started when booking is open.", error.Message);
        Assert.Equal(0, repository.SaveCount);
    }

    [Fact]
    public async Task CloseAsync_WhenRunning_ClosesAndPersists()
    {
        var session = CreateSession(); session.OpenBooking(); session.Start();
        var repository = new StubRepository(session);

        var result = await new CloseSessionService(repository).CloseAsync(session.Id);

        Assert.Equal(SessionStatus.Closed, result.Status);
        Assert.Equal(SessionStatus.Closed, session.Status);
        Assert.Equal(1, repository.SaveCount);
    }

    [Fact]
    public async Task CloseAsync_WhenPaused_RejectsWithoutSaving()
    {
        var session = CreateSession(); session.OpenBooking(); session.Start(); session.Pause();
        var repository = new StubRepository(session);
        var error = await Assert.ThrowsAsync<InvalidOperationException>(() => new CloseSessionService(repository).CloseAsync(session.Id));
        Assert.Equal("Only a running session can be closed.", error.Message);
        Assert.Equal(0, repository.SaveCount);
    }

    private static Session CreateSession()
    {
        var now = DateTime.UtcNow;
        return new Session(Guid.NewGuid(), Guid.NewGuid(), "Lifecycle test", now.AddHours(-1), now.AddMinutes(-30), now.AddHours(2), now.AddHours(1), 10);
    }

    private sealed class StubRepository(Session? session) : ISessionRepository
    {
        public int SaveCount { get; private set; }
        public Task<Session?> GetByIdAsync(Guid sessionId, CancellationToken cancellationToken = default) => Task.FromResult(session?.Id == sessionId ? session : null);
        public Task SaveChangesAsync(CancellationToken cancellationToken = default) { SaveCount++; return Task.CompletedTask; }
        public Task AddAsync(Session value, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }
}

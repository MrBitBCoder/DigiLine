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

        var result = await new SessionLifecycleService(repository).StartAsync(session.Id);

        Assert.Equal(SessionStatus.Running, result.Status);
        Assert.Equal(SessionStatus.Running, session.Status);
        Assert.Equal(1, repository.SaveCount);
    }

    [Fact]
    public async Task StartAsync_WhenScheduled_RejectsWithoutSaving()
    {
        var session = CreateSession(); var repository = new StubRepository(session);
        var error = await Assert.ThrowsAsync<InvalidOperationException>(() => new SessionLifecycleService(repository).StartAsync(session.Id));
        Assert.Equal("A session can only be started when booking is open.", error.Message);
        Assert.Equal(0, repository.SaveCount);
    }

    [Fact]
    public async Task CloseAsync_WhenRunning_ClosesAndPersists()
    {
        var session = CreateSession(); session.OpenBooking(); session.Start(); session.StopBooking(DateTime.UtcNow);
        var repository = new StubRepository(session);

        var result = await new SessionLifecycleService(repository).CloseAsync(session.Id);

        Assert.Equal(SessionStatus.Closed, result.Status);
        Assert.Equal(SessionStatus.Closed, session.Status);
        Assert.Equal(1, repository.SaveCount);
    }

    [Fact]
    public async Task CloseAsync_WhenPaused_CompletesAfterBookingStops()
    {
        var session = CreateSession(); session.OpenBooking(); session.Start(); session.StopBooking(DateTime.UtcNow); session.Pause();
        var repository = new StubRepository(session);
        var result = await new SessionLifecycleService(repository).CloseAsync(session.Id);
        Assert.Equal(SessionStatus.Closed, result.Status);
        Assert.Equal(1, repository.SaveCount);
    }

    [Theory]
    [InlineData(QueueTokenStatus.Waiting)]
    [InlineData(QueueTokenStatus.Called)]
    [InlineData(QueueTokenStatus.InConsultation)]
    [InlineData(QueueTokenStatus.Late)]
    public async Task CompletionBlocksEveryUnfinishedStatus(QueueTokenStatus status)
    {
        var session = CreateSession(); session.OpenBooking(); session.Start(); session.StopBooking(DateTime.UtcNow);
        var token = new QueueToken(session.Id, 1, "Patient", null);
        typeof(QueueToken).GetProperty(nameof(QueueToken.Status))!.SetValue(token, status);
        session.QueueTokens.Add(token);
        var repository = new StubRepository(session);
        var error = await Assert.ThrowsAsync<SessionCompletionException>(() => new SessionLifecycleService(repository).CompleteAsync(session.Id, true));
        Assert.Equal(1, error.Counts[status.ToString()]); Assert.Equal(SessionStatus.Running, session.Status); Assert.Equal(0, repository.SaveCount);
    }
    [Fact]
    public async Task SkippedRequiresConfirmationThenFinalizesWithoutConsultationTimestamp()
    {
        var session = CreateSession(); session.OpenBooking(); session.Start(); session.StopBooking(DateTime.UtcNow);
        var token = new QueueToken(session.Id, 1, "Absent", null); token.Call(DateTime.UtcNow); token.Skip(); session.QueueTokens.Add(token);
        var service = new SessionLifecycleService(new StubRepository(session));
        await Assert.ThrowsAsync<SessionCompletionException>(() => service.CompleteAsync(session.Id, false));
        await service.CompleteAsync(session.Id, true); await service.CompleteAsync(session.Id, true);
        Assert.Equal(QueueTokenStatus.NoShow, token.Status); Assert.NotNull(token.FinalizedAt); Assert.Null(token.CompletedAt);
    }
    [Fact]
    public async Task CompletionRequiresBookingStop()
    {
        var session = CreateSession(); session.OpenBooking(); session.Start();
        await Assert.ThrowsAsync<InvalidOperationException>(() => new SessionLifecycleService(new StubRepository(session)).CompleteAsync(session.Id, true));
        Assert.True(session.IsBookingOpen(DateTime.UtcNow));
    }

    private static Session CreateSession()
    {
        var now = DateTime.UtcNow;
        return new Session(Guid.NewGuid(), Guid.NewGuid(), "Lifecycle test", now.AddHours(-1), now.AddMinutes(-30), now.AddHours(2), now.AddHours(1), 10);
    }

    private sealed class StubRepository(Session? session) : ISessionRepository
    {
        public Task<IReadOnlyList<QueueToken>> GetUnfinishedTokensForUpdateAsync(Guid id, CancellationToken ct = default) => Task.FromResult<IReadOnlyList<QueueToken>>(session!.QueueTokens.ToList());
        public int SaveCount { get; private set; }
        public Task<Session?> GetByIdAsync(Guid sessionId, CancellationToken cancellationToken = default) => Task.FromResult(session?.Id == sessionId ? session : null);
        public Task SaveChangesAsync(CancellationToken cancellationToken = default) { SaveCount++; return Task.CompletedTask; }
        public Task AddAsync(Session value, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }
}

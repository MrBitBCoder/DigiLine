using DigiLine.Application.Patients;
using DigiLine.Application.Queue;
using DigiLine.Application.Sessions;
using DigiLine.Domain.Entities;
using DigiLine.Domain.Common;

namespace DigiLine.Application.Tests;

public class PublicQueueServiceTests
{
    [Fact]
    public async Task BookAsync_WhenActiveBookingExists_ReturnsItWithoutCreatingDuplicate()
    {
        var sessionId = Guid.NewGuid(); var profileId = Guid.NewGuid();
        var existing = new QueueToken(sessionId, 9, profileId);
        var booking = new StubBookingService();
        var service = CreateService(existing, booking, ownsProfile: true);

        var result = await service.BookAsync("access", sessionId, profileId);

        Assert.True(result.WasAlreadyBooked);
        Assert.Equal(existing.Id, result.TokenId);
        Assert.Equal(9, result.TokenNumber);
        Assert.False(booking.WasCalled);
    }

    [Fact]
    public async Task BookAsync_WhenProfileBelongsToAnotherPhone_RejectsWithoutBooking()
    {
        var booking = new StubBookingService();
        var service = CreateService(null, booking, ownsProfile: false);
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            service.BookAsync("access", Guid.NewGuid(), Guid.NewGuid()));
        Assert.False(booking.WasCalled);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task GetTokenAsync_AfterBookingStops_PreservesExistingTokenAndNowServing(bool paused)
    {
        var now = DateTime.UtcNow;
        var session = new Session(Guid.NewGuid(), Guid.NewGuid(), "Live", now.AddHours(-2), now.AddHours(-1), now.AddHours(1), now.AddHours(1), 10);
        session.OpenBooking(); session.Start();
        if (paused) session.Pause();
        session.StopBooking(now);
        var token = new QueueToken(session.Id, 7, Guid.NewGuid());
        token.Call(now);
        var service = new PublicQueueService(new StubAccessService(), new StubPatientRepository(true), new StubBookingService(),
            new StubTokenRepository(token), new StubSessionRepository(session), new StubPositionService(), new StubWaitService());
        var result = await service.GetTokenAsync("access", session.Id, token.Id);
        Assert.NotNull(result);
        Assert.Equal(7, result.TokenNumber);
        Assert.Equal(7, result.CurrentTokenNumber);
        Assert.Equal(paused, result.IsPaused);
        Assert.Equal(paused ? SessionStatus.Paused : SessionStatus.Running, result.SessionStatus);
        Assert.False(session.IsBookingOpen(now));
    }

    [Fact]
    public async Task GetTokenAsync_WhenTokenBelongsToAnotherPatient_DoesNotExposeIt()
    {
        var now = DateTime.UtcNow;
        var session = new Session(Guid.NewGuid(), Guid.NewGuid(), "Live", now.AddHours(-2), now.AddHours(-1), now.AddHours(1), now.AddHours(1), 10);
        session.OpenBooking();
        session.Start();
        session.StopBooking(now);
        var token = new QueueToken(session.Id, 7, Guid.NewGuid());
        var service = new PublicQueueService(new StubAccessService(), new StubPatientRepository(false), new StubBookingService(),
            new StubTokenRepository(token), new StubSessionRepository(session), new StubPositionService(), new StubWaitService());

        var result = await service.GetTokenAsync("access", session.Id, token.Id);

        Assert.Null(result);
    }

    [Fact]
    public async Task RecoverAsync_ReturnsTheOnlyOwnedRecoverableTokenWithoutMutationOrBooking()
    {
        var session = LiveSession(); var sessionId = session.Id; var profileId = Guid.NewGuid();
        var token = new QueueToken(sessionId, 15, profileId);
        token.Call(DateTime.UtcNow); token.Skip(); token.MarkLate();
        var originalStatus = token.Status; var originalOrder = token.QueueOrder;
        var booking = new StubBookingService();
        var service = new PublicQueueService(new StubAccessService(), new StubPatientRepository(true), booking,
            new StubTokenRepository(token, [token]), new StubSessionRepository(session), new StubPositionService(), new StubWaitService());

        var result = await service.RecoverAsync("access", sessionId, profileId);

        Assert.NotNull(result); Assert.Equal(token.Id, result.TokenId); Assert.Equal(15, result.TokenNumber);
        Assert.Equal(originalStatus, token.Status); Assert.Equal(originalOrder, token.QueueOrder); Assert.False(booking.WasCalled);
    }

    [Fact]
    public async Task RecoverAsync_WhenOwnershipFailsOrMatchesAreAmbiguous_ReturnsNothing()
    {
        var session = LiveSession(); var sessionId = session.Id; var profileId = Guid.NewGuid();
        var first = new QueueToken(sessionId, 1, profileId); var second = new QueueToken(sessionId, 2, profileId);
        var notOwned = new PublicQueueService(new StubAccessService(), new StubPatientRepository(false), new StubBookingService(),
            new StubTokenRepository(first, [first]), new StubSessionRepository(session), new StubPositionService(), new StubWaitService());
        var ambiguous = new PublicQueueService(new StubAccessService(), new StubPatientRepository(true), new StubBookingService(),
            new StubTokenRepository(first, [first, second]), new StubSessionRepository(session), new StubPositionService(), new StubWaitService());

        Assert.Null(await notOwned.RecoverAsync("access", sessionId, profileId));
        Assert.Null(await ambiguous.RecoverAsync("access", sessionId, profileId));
    }

    [Fact]
    public async Task RecoverAsync_WithExpiredAccess_IsRejectedBeforeLookup()
    {
        var service = new PublicQueueService(new StubAccessService(), new StubPatientRepository(true), new StubBookingService(),
            new StubTokenRepository(null), new StubSessionRepository(), new StubPositionService(), new StubWaitService());
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => service.RecoverAsync("expired", Guid.NewGuid(), Guid.NewGuid()));
    }

    private static Session LiveSession()
    {
        var now = DateTime.UtcNow;
        var session = new Session(Guid.NewGuid(), Guid.NewGuid(), "Live", now.AddHours(-2), now.AddHours(-1), now.AddHours(1), now.AddHours(1), 10);
        session.OpenBooking(); session.Start(); session.StopBooking(now);
        return session;
    }

    private static PublicQueueService CreateService(QueueToken? token, StubBookingService booking, bool ownsProfile) =>
        new(new StubAccessService(), new StubPatientRepository(ownsProfile), booking,
            new StubTokenRepository(token), new StubSessionRepository(),
            new StubPositionService(), new StubWaitService());

    private sealed class StubAccessService : IPatientAccessService
    {
        public string? GetPhoneNumber(string accessToken) => accessToken == "access" ? "9876543210" : null;
        public PhoneVerificationChallenge CreateChallenge(string phoneNumber) => throw new NotSupportedException();
        public string? Verify(Guid challengeId, string code) => throw new NotSupportedException();
        public string IssueAccess(string phoneNumber) => throw new NotSupportedException();
    }
    private sealed class StubPatientRepository(bool owns) : IPublicPatientRepository
    {
        public Task<bool> IsOwnedByPhoneAsync(Guid profileId, string phoneNumber, CancellationToken cancellationToken = default) => Task.FromResult(owns);
        public Task<IReadOnlyList<PublicPatientProfile>> ListByPhoneAsync(string phoneNumber, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<PublicPatientProfile> CreateAsync(string phoneNumber, string name, int age, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }
    private sealed class StubBookingService : IBookTokenService
    {
        public bool WasCalled { get; private set; }
        public Task<int> BookAsync(Guid sessionId, Guid patientProfileId, CancellationToken cancellationToken = default) { WasCalled = true; return Task.FromResult(10); }
    }
    private sealed class StubTokenRepository(QueueToken? token, IReadOnlyList<QueueToken>? recoverable = null) : IQueueTokenRepository
    {
        public Task<QueueToken?> GetActiveByPatientAsync(Guid sessionId, Guid patientProfileId, CancellationToken cancellationToken = default) => Task.FromResult(token);
        public Task<IReadOnlyList<QueueToken>> GetRecoverableByPatientAsync(Guid sessionId, Guid patientProfileId, CancellationToken cancellationToken = default) => Task.FromResult(recoverable ?? []);
        public Task<QueueToken?> GetByIdAsync(Guid sessionId, Guid tokenId, CancellationToken cancellationToken = default) => Task.FromResult(token?.SessionId == sessionId && token.Id == tokenId ? token : null);
        public Task<IReadOnlyList<QueueToken>> GetBySessionOrderedAsync(Guid sessionId, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<QueueToken>>(token is null ? [] : [token]);
        public Task<QueueToken?> GetNextWaitingForUpdateAsync(Guid sessionId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<int?> GetMaximumTokenNumberAsync(Guid sessionId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<int?> AllocateNextTokenNumberAsync(Guid sessionId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task AddAsync(QueueToken queueToken, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task SaveChangesAsync(CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }
    private sealed class StubSessionRepository(Session? session = null) : ISessionRepository
    {
        public Task<Session?> GetByIdAsync(Guid sessionId, CancellationToken cancellationToken = default) => Task.FromResult(session);
        public Task AddAsync(Session session, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task SaveChangesAsync(CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }
    private sealed class StubPositionService : IGetQueuePositionService { public Task<QueuePositionResult> GetAsync(Guid sessionId, Guid tokenId, CancellationToken cancellationToken = default) => throw new NotSupportedException(); }
    private sealed class StubWaitService : IGetEstimatedWaitService { public Task<EstimatedWaitResult> GetAsync(Guid sessionId, Guid tokenId, CancellationToken cancellationToken = default) => throw new NotSupportedException(); }
}

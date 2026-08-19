using DigiLine.Application.Patients;
using DigiLine.Application.Queue;
using DigiLine.Application.Sessions;
using DigiLine.Domain.Entities;

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
    private sealed class StubTokenRepository(QueueToken? token) : IQueueTokenRepository
    {
        public Task<QueueToken?> GetActiveByPatientAsync(Guid sessionId, Guid patientProfileId, CancellationToken cancellationToken = default) => Task.FromResult(token);
        public Task<QueueToken?> GetByIdAsync(Guid sessionId, Guid tokenId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<IReadOnlyList<QueueToken>> GetBySessionOrderedAsync(Guid sessionId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<QueueToken?> GetNextWaitingForUpdateAsync(Guid sessionId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<int?> GetMaximumTokenNumberAsync(Guid sessionId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<int?> AllocateNextTokenNumberAsync(Guid sessionId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task AddAsync(QueueToken queueToken, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task SaveChangesAsync(CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }
    private sealed class StubSessionRepository : ISessionRepository
    {
        public Task<Session?> GetByIdAsync(Guid sessionId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task AddAsync(Session session, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task SaveChangesAsync(CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }
    private sealed class StubPositionService : IGetQueuePositionService { public Task<QueuePositionResult> GetAsync(Guid sessionId, Guid tokenId, CancellationToken cancellationToken = default) => throw new NotSupportedException(); }
    private sealed class StubWaitService : IGetEstimatedWaitService { public Task<EstimatedWaitResult> GetAsync(Guid sessionId, Guid tokenId, CancellationToken cancellationToken = default) => throw new NotSupportedException(); }
}

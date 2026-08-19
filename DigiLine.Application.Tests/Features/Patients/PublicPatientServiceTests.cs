using DigiLine.Application.Patients;

namespace DigiLine.Application.Tests;

public class PublicPatientServiceTests
{
    [Fact]
    public async Task Profiles_AreReturnedOnlyAfterSuccessfulPhoneVerification()
    {
        var access = new StubAccessService();
        var repository = new StubRepository([new(Guid.NewGuid(), "Asha", 29)]);
        var service = new PublicPatientService(access, repository);
        var challenge = service.RequestVerification("+91 98765 43210");

        Assert.Null(await service.VerifyAsync(challenge.ChallengeId, "000000"));

        challenge = service.RequestVerification("+91 98765 43210");
        var verified = await service.VerifyAsync(challenge.ChallengeId, challenge.VerificationCode);
        Assert.NotNull(verified);
        Assert.Single(verified.Profiles);
        Assert.Equal("919876543210", access.GetPhoneNumber(verified.AccessToken));
    }

    [Fact]
    public void RequestVerification_RejectsInvalidPhoneWithoutAccountDisclosure()
    {
        var service = new PublicPatientService(new StubAccessService(), new StubRepository([]));
        Assert.Throws<InvalidOperationException>(() => service.RequestVerification("123"));
    }

    private sealed class StubRepository(IReadOnlyList<PublicPatientProfile> profiles) : IPublicPatientRepository
    {
        public Task<IReadOnlyList<PublicPatientProfile>> ListByPhoneAsync(string phoneNumber, CancellationToken cancellationToken = default) => Task.FromResult(profiles);
        public Task<PublicPatientProfile> CreateAsync(string phoneNumber, string name, int age, CancellationToken cancellationToken = default) => Task.FromResult(new PublicPatientProfile(Guid.NewGuid(), name, age));
        public Task<bool> IsOwnedByPhoneAsync(Guid profileId, string phoneNumber, CancellationToken cancellationToken = default) => Task.FromResult(profiles.Any(profile => profile.Id == profileId));
    }

    private sealed class StubAccessService : IPatientAccessService
    {
        private readonly Dictionary<Guid, (string Phone, string Code)> _challenges = [];
        private readonly Dictionary<string, string> _tickets = [];
        public PhoneVerificationChallenge CreateChallenge(string phoneNumber) { var id = Guid.NewGuid(); _challenges[id] = (phoneNumber, "123456"); return new(id, "123456", DateTime.UtcNow.AddMinutes(10)); }
        public string? Verify(Guid challengeId, string code) { if (!_challenges.Remove(challengeId, out var value) || value.Code != code) return null; return value.Phone; }
        public string IssueAccess(string phoneNumber) { var token = Guid.NewGuid().ToString("N"); _tickets[token] = phoneNumber; return token; }
        public string? GetPhoneNumber(string accessToken) => _tickets.GetValueOrDefault(accessToken);
    }
}

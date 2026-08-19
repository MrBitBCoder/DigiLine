namespace DigiLine.Application.Patients;

public interface IPublicPatientService
{
    PhoneVerificationChallenge RequestVerification(string phoneNumber);
    Task<VerifiedPatientAccess?> VerifyAsync(Guid challengeId, string code, CancellationToken cancellationToken = default);
    Task<PublicPatientProfile> CreateProfileAsync(string accessToken, string name, int age, CancellationToken cancellationToken = default);
}

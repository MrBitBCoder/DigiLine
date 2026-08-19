namespace DigiLine.Application.Patients;

public sealed record PublicPatientProfile(Guid Id, string Name, int Age);
public sealed record PhoneVerificationChallenge(Guid ChallengeId, string VerificationCode, DateTime ExpiresAt);
public sealed record VerifiedPatientAccess(string AccessToken, DateTime ExpiresAt, IReadOnlyList<PublicPatientProfile> Profiles);

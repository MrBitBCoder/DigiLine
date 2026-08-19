namespace DigiLine.Application.Patients;

public sealed class PublicPatientService : IPublicPatientService
{
    private readonly IPatientAccessService _access;
    private readonly IPublicPatientRepository _patients;
    private readonly TimeProvider _clock;

    public PublicPatientService(IPatientAccessService access, IPublicPatientRepository patients, TimeProvider? clock = null)
    { _access = access; _patients = patients; _clock = clock ?? TimeProvider.System; }

    public PhoneVerificationChallenge RequestVerification(string phoneNumber) =>
        _access.CreateChallenge(NormalizePhone(phoneNumber));

    public async Task<VerifiedPatientAccess?> VerifyAsync(Guid challengeId, string code, CancellationToken cancellationToken = default)
    {
        var phone = _access.Verify(challengeId, code);
        if (phone is null) return null;
        var token = _access.IssueAccess(phone);
        var profiles = await _patients.ListByPhoneAsync(phone, cancellationToken);
        return new VerifiedPatientAccess(token, _clock.GetUtcNow().UtcDateTime.AddHours(12), profiles);
    }

    public async Task<PublicPatientProfile> CreateProfileAsync(string accessToken, string name, int age, CancellationToken cancellationToken = default)
    {
        var phone = _access.GetPhoneNumber(accessToken)
            ?? throw new UnauthorizedAccessException("Patient access has expired.");
        if (string.IsNullOrWhiteSpace(name) || name.Trim().Length > 200)
            throw new InvalidOperationException("Patient name is required and cannot exceed 200 characters.");
        if (age is < 0 or > 130) throw new InvalidOperationException("Enter a valid patient age.");
        return await _patients.CreateAsync(phone, name.Trim(), age, cancellationToken);
    }

    private static string NormalizePhone(string value)
    {
        var normalized = new string((value ?? string.Empty).Where(char.IsDigit).ToArray());
        if (normalized.Length is < 7 or > 15) throw new InvalidOperationException("Enter a valid phone number.");
        return normalized;
    }
}

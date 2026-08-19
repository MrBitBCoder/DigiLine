namespace DigiLine.Application.Patients;

public interface IPatientAccessService
{
    PhoneVerificationChallenge CreateChallenge(string phoneNumber);
    string? Verify(Guid challengeId, string code);
    string IssueAccess(string phoneNumber);
    string? GetPhoneNumber(string accessToken);
}

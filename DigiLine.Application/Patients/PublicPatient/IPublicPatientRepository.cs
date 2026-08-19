namespace DigiLine.Application.Patients;

public interface IPublicPatientRepository
{
    Task<IReadOnlyList<PublicPatientProfile>> ListByPhoneAsync(string phoneNumber, CancellationToken cancellationToken = default);
    Task<PublicPatientProfile> CreateAsync(string phoneNumber, string name, int age, CancellationToken cancellationToken = default);
    Task<bool> IsOwnedByPhoneAsync(Guid profileId, string phoneNumber, CancellationToken cancellationToken = default);
}

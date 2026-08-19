namespace DigiLine.Application.Clinics;

public interface IGetPublicClinicService
{
    Task<PublicClinicDetails?> GetAsync(
        string clinicSlug,
        CancellationToken cancellationToken = default);
}

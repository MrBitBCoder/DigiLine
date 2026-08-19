namespace DigiLine.Application.Clinics;

public interface IPublicClinicRepository
{
    Task<PublicClinicRecord?> GetBySlugAsync(
        string slug,
        DateTime sessionsEndingAfter,
        CancellationToken cancellationToken = default);
}

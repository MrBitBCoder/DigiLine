namespace DigiLine.Application.Sessions;

public interface IGetDoctorSessionsService
{
    Task<IReadOnlyList<DoctorSessionSummary>> ListAsync(Guid clinicId, Guid doctorId, CancellationToken cancellationToken = default);
    Task<DoctorSessionDetails?> GetAsync(Guid sessionId, Guid clinicId, Guid doctorId, CancellationToken cancellationToken = default);
    Task<DoctorSessionDetails?> GetActiveOrUpcomingAsync(Guid clinicId, Guid doctorId, CancellationToken cancellationToken = default);
}

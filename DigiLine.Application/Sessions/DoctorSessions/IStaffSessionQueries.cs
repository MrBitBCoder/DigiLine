using DigiLine.Application.Administration;
using DigiLine.Domain.Common;
namespace DigiLine.Application.Sessions;
public sealed class SessionPageQuery
{
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 10;
    public string? Name { get; set; }
    public DateOnly? FromDate { get; set; }
    public DateOnly? ToDate { get; set; }
    public SessionStatus? Status { get; set; }
}
public interface IStaffSessionQueries
{
    Task<PageResult<DoctorSessionSummary>> ListAsync(Guid clinicId, Guid? doctorId, SessionPageQuery query, CancellationToken ct);
    Task<DoctorSessionDetails?> DetailsAsync(Guid clinicId, Guid? doctorId, Guid sessionId, int page, int pageSize, string filter, CancellationToken ct);
    Task<DoctorSessionDetails?> ActiveAsync(Guid clinicId, Guid doctorId, CancellationToken ct);
    Task<bool> OwnsAsync(Guid clinicId, Guid? doctorId, Guid sessionId, CancellationToken ct);
}

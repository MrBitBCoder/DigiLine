using DigiLine.Domain.Common;
namespace DigiLine.Application.Administration;

public sealed record RegisterClinicRequest(string ClinicName, string Slug, string TimeZoneId, string Name, string Email, string Password, bool AlsoDoctor);
public sealed record StaffUpdateRequest(string Name, StaffRole[] Roles, bool IsActive);
public sealed record CreateStaffRequest(string Name, string Email, StaffRole[] Roles, string Password);
public sealed record InviteStaffRequest(string Name, string Email, StaffRole[] Roles);
public sealed record AcceptInvitationRequest(string Token, string Password);
public sealed record ClinicUpdateRequest(string Name, string TimeZoneId);
public sealed record StaffView(Guid Id, string Name, string Email, StaffRole[] Roles, bool IsActive, bool InvitationPending, bool DeliveryFailed);
public sealed record PageResult<T>(IReadOnlyList<T> Items, int Page, int PageSize, int TotalCount);
public interface IAdministrationService
{
    Task RegisterAsync(RegisterClinicRequest request, CancellationToken ct);
    Task<object> GetClinicAsync(Guid clinicId, CancellationToken ct);
    Task UpdateClinicAsync(Guid clinicId, Guid actorId, ClinicUpdateRequest request, CancellationToken ct);
    Task<PageResult<StaffView>> ListAsync(Guid clinicId, int page, int pageSize, CancellationToken ct);
    Task<StaffView> CreateAsync(Guid clinicId, Guid actorId, CreateStaffRequest request, CancellationToken ct);
    Task<StaffView> InviteAsync(Guid clinicId, Guid actorId, InviteStaffRequest request, CancellationToken ct);
    Task UpdateAsync(Guid clinicId, Guid actorId, Guid staffId, StaffUpdateRequest request, CancellationToken ct);
    Task<bool> ResendAsync(Guid clinicId, Guid actorId, Guid staffId, CancellationToken ct);
    Task RevokeAsync(Guid clinicId, Guid actorId, Guid staffId, CancellationToken ct);
    Task AcceptAsync(AcceptInvitationRequest request, CancellationToken ct);
}

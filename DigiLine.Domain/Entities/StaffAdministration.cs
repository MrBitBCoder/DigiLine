using DigiLine.Domain.Common;

namespace DigiLine.Domain.Entities;

public sealed class StaffRoleAssignment
{
    public Guid StaffId { get; private set; }
    public StaffRole Role { get; private set; }
    private StaffRoleAssignment() { }
    public StaffRoleAssignment(Guid staffId, StaffRole role) { StaffId = staffId; Role = role; }
}

public sealed class StaffInvitation
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid StaffId { get; set; }
    public string TokenHash { get; set; } = "";
    public DateTime ExpiresAt { get; set; }
    public DateTime? UsedAt { get; set; }
    public bool DeliveryFailed { get; set; }
}

public sealed class AdministrationAudit
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ClinicId { get; set; }
    public Guid ActorId { get; set; }
    public Guid? StaffId { get; set; }
    public string Action { get; set; } = "";
    public DateTime OccurredAt { get; set; } = DateTime.UtcNow;
}

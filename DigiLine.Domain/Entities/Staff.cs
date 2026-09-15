using DigiLine.Domain.Common;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DigiLine.Domain.Entities
{

    public class Staff
    {
        public Guid Id { get; private set; }

        public Guid ClinicId { get; private set; }

        public Clinic Clinic { get; private set; } = null!;

        public string Name { get; private set; } = string.Empty;

        public string Email { get; private set; } = string.Empty;

        public string PasswordHash { get; private set; } = string.Empty;
        public Guid PasswordVersion { get; private set; } = Guid.NewGuid();
        public string NormalizedEmail { get; private set; } = string.Empty;

        public StaffRole Role { get; private set; }
        public ICollection<StaffRoleAssignment> Roles { get; private set; } = new List<StaffRoleAssignment>();
        public bool IsActive { get; private set; } = true;
        public bool InvitationPending { get; private set; }

        public void UpdateAccess(string name, IEnumerable<StaffRole> roles, bool active)
        {
            Name = name.Trim();
            var selected = roles.Distinct().ToArray();
            if (selected.Length == 0 || selected.Any(role => !Enum.IsDefined(role)))
                throw new ArgumentException("Choose at least one valid role.");
            foreach (var assignment in Roles.Where(x => !selected.Contains(x.Role)).ToArray()) Roles.Remove(assignment);
            foreach (var role in selected.Where(role => !Roles.Any(x => x.Role == role)))
                Roles.Add(new StaffRoleAssignment(Id, role));
            Role = selected[0]; // Retained for migration compatibility; authorization uses Roles.
            IsActive = active;
            PasswordVersion = Guid.NewGuid();
            UpdatedAt = DateTime.UtcNow;
        }

        public void AwaitInvitation() { InvitationPending = true; IsActive = false; }
        public void AcceptInvitation(string hash)
        {
            ChangePassword(hash);
            InvitationPending = false;
            IsActive = true;
        }

        public DateTime CreatedAt { get; private set; }

        public DateTime UpdatedAt { get; private set; }

        private Staff()
        {
        }

        public Staff(
            Guid clinicId,
            string name,
            string email,
            string passwordHash,
            StaffRole role)
        {
            Id = Guid.NewGuid();
            ClinicId = clinicId;
            Name = name;
            Email = email.Trim();
            NormalizedEmail = email.Trim().ToUpperInvariant();
            PasswordHash = passwordHash;
            Role = role;
            Roles.Add(new StaffRoleAssignment(Id, role));
            CreatedAt = DateTime.UtcNow;
            UpdatedAt = DateTime.UtcNow;
        }

        public void ChangePassword(string passwordHash)
        {
            PasswordHash = passwordHash;
            PasswordVersion = Guid.NewGuid();
            UpdatedAt = DateTime.UtcNow;
        }
    }
}

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

        public StaffRole Role { get; private set; }

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
            Email = email;
            PasswordHash = passwordHash;
            Role = role;
            CreatedAt = DateTime.UtcNow;
            UpdatedAt = DateTime.UtcNow;
        }
    }
}

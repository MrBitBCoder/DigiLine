using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DigiLine.Domain.Entities
{
    public class PatientProfile
    {
        public Guid Id { get; private set; }

        public Guid PhoneAccountId { get; private set; }

        public PhoneAccount PhoneAccount { get; private set; } = null!;

        public string Name { get; private set; } = string.Empty;

        public int Age { get; private set; }

        public DateTime CreatedAt { get; private set; }

        public DateTime UpdatedAt { get; private set; }

        private PatientProfile()
        {
        }

        public PatientProfile(
            Guid phoneAccountId,
            string name,
            int age)
        {
            Id = Guid.NewGuid();
            PhoneAccountId = phoneAccountId;
            Name = name;
            Age = age;
            CreatedAt = DateTime.UtcNow;
            UpdatedAt = DateTime.UtcNow;
        }
    }
}

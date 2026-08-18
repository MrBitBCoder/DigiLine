using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DigiLine.Domain.Entities
{
    public class PhoneAccount
    {
        public Guid Id { get; private set; }

        public string PhoneNumber { get; private set; } = string.Empty;

        public DateTime CreatedAt { get; private set; }

        public DateTime UpdatedAt { get; private set; }

        private PhoneAccount()
        {
        }

        public PhoneAccount(string phoneNumber)
        {
            Id = Guid.NewGuid();
            PhoneNumber = phoneNumber;
            CreatedAt = DateTime.UtcNow;
            UpdatedAt = DateTime.UtcNow;
        }
    }
}

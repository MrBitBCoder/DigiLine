using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DigiLine.Domain.Entities
{
    public class Clinic
    {
        public Guid Id { get; private set; }

        public string Name { get; private set; } = string.Empty;

        public string Slug { get; private set; } = string.Empty;

        public DateTime CreatedAt { get; private set; }

        public DateTime UpdatedAt { get; private set; }

        private Clinic()
        {
        }

        public Clinic(string name, string slug)
        {
            Id = Guid.NewGuid();
            Name = name;
            Slug = slug;
            CreatedAt = DateTime.UtcNow;
            UpdatedAt = DateTime.UtcNow;
        }
    }
}

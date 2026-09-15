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

        public string TimeZoneId { get; private set; } = "Asia/Kolkata";

        public DateTime CreatedAt { get; private set; }

        public DateTime UpdatedAt { get; private set; }

        private Clinic()
        {
        }

        public Clinic(string name, string slug, string timeZoneId = "Asia/Kolkata")
        {
            Id = Guid.NewGuid();
            Name = name;
            Slug = slug;
            SetTimeZone(timeZoneId);
            CreatedAt = DateTime.UtcNow;
            UpdatedAt = DateTime.UtcNow;
        }

        public void SetTimeZone(string timeZoneId)
        {
            if (string.IsNullOrWhiteSpace(timeZoneId))
                throw new ArgumentException("A clinic time zone is required.", nameof(timeZoneId));
            _ = TimeZoneInfo.FindSystemTimeZoneById(timeZoneId);
            TimeZoneId = timeZoneId;
        }

        public void Rename(string name)
        {
            Name = name.Trim();
            UpdatedAt = DateTime.UtcNow;
        }
    }
}

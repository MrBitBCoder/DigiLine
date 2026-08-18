using DigiLine.Domain.Common;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DigiLine.Domain.Entities
{
    public class QueueToken
    {

        public QueueToken(
                Guid sessionId,
                int tokenNumber,
                Guid? patientProfileId)
        {
            Id = Guid.NewGuid();

            SessionId = sessionId;
            TokenNumber = tokenNumber;
            PatientProfileId = patientProfileId;

            Status = QueueTokenStatus.Waiting;

            CreatedAt = DateTime.UtcNow;
        }

        public Guid Id { get; private set; }

        public Guid SessionId { get; private set; }
        public Session Session { get; private set; } = null!;

        public int TokenNumber { get; private set; }

        public Guid? PatientProfileId { get; private set; }
        public PatientProfile? PatientProfile { get; private set; }

        public string? WalkInName { get; private set; }
        public string? PhoneNumber { get; private set; }

        public QueueTokenStatus Status { get; private set; }

        public DateTime CreatedAt { get; private set; }

        public DateTime? CalledAt { get; private set; }

        public DateTime? StartedConsultationAt { get; private set; }

        public DateTime? CompletedAt { get; private set; }
    }
}

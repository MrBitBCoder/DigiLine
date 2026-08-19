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

        public QueueToken(
            Guid sessionId,
            int tokenNumber,
            string walkInName,
            string? phoneNumber)
        {
            if (string.IsNullOrWhiteSpace(walkInName))
                throw new InvalidOperationException(
                    "Walk-in name is required.");

            var normalizedWalkInName = walkInName.Trim();

            if (normalizedWalkInName.Length > 200)
                throw new InvalidOperationException(
                    "Walk-in name cannot exceed 200 characters.");

            var normalizedPhoneNumber = string.IsNullOrWhiteSpace(phoneNumber)
                ? null
                : phoneNumber.Trim();

            if (normalizedPhoneNumber?.Length > 20)
                throw new InvalidOperationException(
                    "Phone number cannot exceed 20 characters.");

            Id = Guid.NewGuid();

            SessionId = sessionId;
            TokenNumber = tokenNumber;

            PatientProfileId = null;

            WalkInName = normalizedWalkInName;
            PhoneNumber = normalizedPhoneNumber;

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

        public void Call(DateTime calledAt)
        {
            if (Status != QueueTokenStatus.Waiting
                && Status != QueueTokenStatus.Late)
                throw new InvalidOperationException(
                    "Only a waiting or late token can be called.");

            Status = QueueTokenStatus.Called;
            CalledAt = calledAt;
        }

        public void MarkLate()
        {
            if (Status != QueueTokenStatus.Skipped)
                throw new InvalidOperationException(
                    "Only a skipped token can be marked late.");

            Status = QueueTokenStatus.Late;
        }

        public void Recall(DateTime recalledAt)
        {
            if (Status != QueueTokenStatus.Called)
                throw new InvalidOperationException(
                    "Only a called token can be recalled.");

            CalledAt = recalledAt;
        }

        public void Skip()
        {
            if (Status != QueueTokenStatus.Called)
                throw new InvalidOperationException(
                    "Only a called token can be skipped.");

            Status = QueueTokenStatus.Skipped;
        }

        public void Complete(DateTime completedAt)
        {
            if (Status != QueueTokenStatus.Called
                && Status != QueueTokenStatus.InConsultation)
                throw new InvalidOperationException(
                    "Only a called or in-consultation token can be completed.");

            Status = QueueTokenStatus.Completed;
            CompletedAt = completedAt;
        }

        public void Cancel()
        {
            if (Status != QueueTokenStatus.Waiting)
                throw new InvalidOperationException(
                    "Only a waiting token can be cancelled.");

            Status = QueueTokenStatus.Cancelled;
        }
    }
}

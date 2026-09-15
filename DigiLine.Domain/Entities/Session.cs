using DigiLine.Domain.Common;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DigiLine.Domain.Entities
{
    public class Session
    {
        public Guid Id { get; private set; }

        public Guid ClinicId { get; private set; }
        public Clinic Clinic { get; private set; } = null!;

        public ICollection<QueueToken> QueueTokens { get; private set; } = new List<QueueToken>();

        public Guid DoctorId { get; private set; }
        public Staff Doctor { get; private set; } = null!;

        public string Name { get; private set; } = string.Empty;

        public DateTime BookingOpenTime { get; private set; }
        public DateTime StartTime { get; private set; }
        public DateTime EndTime { get; private set; }
        public DateTime LastBookingTime { get; private set; }

        public int NextTokenNumber { get; private set; }
        public long NextQueueOrder { get; private set; }

        public int AverageConsultationMinutes { get; private set; }

        public SessionStatus Status { get; private set; }
        public DateTime? BookingStoppedAt { get; private set; }
        public DateTime? CompletedAt { get; private set; }

        public DateTime CreatedAt { get; private set; }
        public DateTime UpdatedAt { get; private set; }

        private Session()
        {
        }

        public Session(
            Guid clinicId,
            Guid doctorId,
            string name,
            DateTime bookingOpenTime,
            DateTime startTime,
            DateTime endTime,
            DateTime lastBookingTime,
            int averageConsultationMinutes)
        {
            if (bookingOpenTime >= startTime)
                throw new InvalidOperationException(
                    "Booking open time must be before start time.");

            if (startTime >= endTime)
                throw new InvalidOperationException(
                    "Start time must be before end time.");

            if (lastBookingTime > endTime)
                throw new InvalidOperationException(
                    "Last booking time must be on or before end time.");

            if (averageConsultationMinutes <= 0)
                throw new InvalidOperationException(
                    "Average consultation time must be greater than zero.");


            Id = Guid.NewGuid();

            ClinicId = clinicId;
            DoctorId = doctorId;
            Name = name;

            BookingOpenTime = bookingOpenTime;
            StartTime = startTime;
            EndTime = endTime;
            LastBookingTime = lastBookingTime;

            NextTokenNumber = 1;
            NextQueueOrder = 1;

            AverageConsultationMinutes = averageConsultationMinutes;

            Status = SessionStatus.Scheduled;

            CreatedAt = DateTime.UtcNow;
            UpdatedAt = DateTime.UtcNow;
        }


        public bool IsBookingOpen(DateTime currentTime)
        {
            return BookingStoppedAt == null && SessionBookingAvailability.IsOpen(Status, BookingOpenTime, LastBookingTime, EndTime, currentTime);
        }

        public void OpenBooking()
        {
            if (Status != SessionStatus.Scheduled)
                throw new InvalidOperationException(
                    "Booking can only be opened for a scheduled session.");

            Status = SessionStatus.BookingOpen;
            UpdatedAt = DateTime.UtcNow;
        }

        public void Reschedule(string name, DateTime bookingOpenTime, DateTime startTime, DateTime endTime, int averageConsultationMinutes)
        {
            if (Status is not (SessionStatus.Scheduled or SessionStatus.BookingOpen) || QueueTokens.Count != 0)
                throw new InvalidOperationException("Only sessions without tokens that have not started can be edited.");
            if (string.IsNullOrWhiteSpace(name) || name.Trim().Length > 200)
                throw new InvalidOperationException("A session name of at most 200 characters is required.");
            if (startTime <= DateTime.UtcNow || endTime <= startTime)
                throw new InvalidOperationException("Choose a future start time and an end time after it.");
            if (bookingOpenTime >= startTime)
                throw new InvalidOperationException("Booking open time must be before start time.");
            if (averageConsultationMinutes is < 1 or > 240)
                throw new InvalidOperationException("Average visit must be between 1 and 240 minutes.");
            Name = name.Trim();
            BookingOpenTime = bookingOpenTime;
            StartTime = startTime;
            EndTime = endTime;
            LastBookingTime = endTime;
            UpdatedAt = DateTime.UtcNow;
            AverageConsultationMinutes = averageConsultationMinutes;
        }

        public void EnsureCanDelete()
        {
            if (Status is not (SessionStatus.Scheduled or SessionStatus.BookingOpen) || QueueTokens.Count != 0)
                throw new InvalidOperationException("Only sessions without tokens that have not started can be deleted.");
        }

        public void Start()
        {
            if (Status != SessionStatus.BookingOpen)
                throw new InvalidOperationException(
                    "A session can only be started when booking is open.");

            Status = SessionStatus.Running;
            UpdatedAt = DateTime.UtcNow;
        }

        public void Pause()
        {
            if (Status != SessionStatus.Running)
                throw new InvalidOperationException(
                    "Only a running session can be paused.");

            Status = SessionStatus.Paused;
            UpdatedAt = DateTime.UtcNow;
        }

        public void Resume()
        {
            if (Status != SessionStatus.Paused)
                throw new InvalidOperationException(
                    "Only a paused session can be resumed.");

            Status = SessionStatus.Running;
            UpdatedAt = DateTime.UtcNow;
        }

        public void StopBooking(DateTime now)
        {
            if (Status == SessionStatus.Closed) throw new InvalidOperationException("This session is already completed.");
            BookingStoppedAt ??= now;
            UpdatedAt = now;
        }

        public void CompleteSession(DateTime now)
        {
            if (Status == SessionStatus.Closed) return;
            if (Status is not (SessionStatus.Running or SessionStatus.Paused))
                throw new InvalidOperationException("Only running or paused sessions can be completed.");
            if (BookingStoppedAt == null && now < LastBookingTime && now < EndTime)
                throw new InvalidOperationException("Stop booking before completing this session.");
            Status = SessionStatus.Closed;
            CompletedAt = now;
            UpdatedAt = now;
        }

        [Obsolete("Use the guarded session lifecycle service.")]
        public void Close() => throw new InvalidOperationException("Use explicit session completion after resolving the queue.");
    }
}

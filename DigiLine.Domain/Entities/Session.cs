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

        public int AverageConsultationMinutes { get; private set; }

        public SessionStatus Status { get; private set; }

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

            AverageConsultationMinutes = averageConsultationMinutes;

            Status = SessionStatus.Scheduled;

            CreatedAt = DateTime.UtcNow;
            UpdatedAt = DateTime.UtcNow;
        }


        public bool IsBookingOpen(DateTime currentTime)
        {
            return currentTime >= BookingOpenTime
                && currentTime <= LastBookingTime;
        }

        public void OpenBooking()
        {
            if (Status != SessionStatus.Scheduled)
                throw new InvalidOperationException(
                    "Booking can only be opened for a scheduled session.");

            Status = SessionStatus.BookingOpen;
            UpdatedAt = DateTime.UtcNow;
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

        public void Close()
        {
            if (Status != SessionStatus.Running)
                throw new InvalidOperationException(
                    "Only a running session can be closed.");

            Status = SessionStatus.Closed;
            UpdatedAt = DateTime.UtcNow;
        }
    }
}

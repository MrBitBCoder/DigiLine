using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DigiLine.Application.Sessions
{

    public class CreateSessionRequest
    {
        public Guid ClinicId { get; set; }

        public Guid DoctorId { get; set; }

        public string Name { get; set; } = string.Empty;

        public DateTime BookingOpenTime { get; set; }

        public DateTime StartTime { get; set; }

        public DateTime EndTime { get; set; }

        public DateTime LastBookingTime { get; set; }

        public int AverageConsultationMinutes { get; set; }
    }
}

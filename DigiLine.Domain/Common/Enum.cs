using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DigiLine.Domain.Common
{
    public enum StaffRole
    {
        Doctor = 1,
        Receptionist = 2
    }

    public enum SessionStatus
    {
        Scheduled = 1,
        BookingOpen = 2,
        Running = 3,
        Paused = 4,
        Closed = 5
    }


    public enum QueueTokenStatus
    {
        Waiting = 1,
        Called = 2,
        InConsultation = 3,
        Completed = 4,
        Skipped = 5,
        Late = 6,
        Cancelled = 7,
        NoShow = 8
    }
}

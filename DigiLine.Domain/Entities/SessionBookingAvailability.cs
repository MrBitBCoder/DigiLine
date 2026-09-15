using DigiLine.Domain.Common;

namespace DigiLine.Domain.Entities;

public static class SessionBookingAvailability
{
    public static bool IsOpen(SessionStatus status, DateTime bookingOpenTime, DateTime lastBookingTime,
        DateTime endTime, DateTime now) =>
        status is SessionStatus.BookingOpen or SessionStatus.Running or SessionStatus.Paused
        && now >= bookingOpenTime
        && now < lastBookingTime
        && now < endTime;
}

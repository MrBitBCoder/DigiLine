using DigiLine.Domain.Common;
using DigiLine.Domain.Entities;

namespace DigiLine.Domain.Tests.Entities;

public class SessionBookingAvailabilityTests
{
    private static readonly DateTime Start = new(2026, 9, 14, 8, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void OnlyPublishedOrActiveStatusesAcceptBookings()
    {
        var open = Start.AddHours(-1);
        var close = Start.AddHours(1);
        Assert.False(SessionBookingAvailability.IsOpen(SessionStatus.Scheduled, open, close, close, Start));
        Assert.True(SessionBookingAvailability.IsOpen(SessionStatus.BookingOpen, open, close, close, Start));
        Assert.True(SessionBookingAvailability.IsOpen(SessionStatus.Running, open, close, close, Start));
        Assert.True(SessionBookingAvailability.IsOpen(SessionStatus.Paused, open, close, close, Start));
        Assert.False(SessionBookingAvailability.IsOpen(SessionStatus.Closed, open, close, close, Start));
    }

    [Fact]
    public void EndTime_IsExclusiveEvenWhenLastBookingTimeIsLater()
    {
        Assert.True(SessionBookingAvailability.IsOpen(SessionStatus.Running, Start.AddHours(-1), Start.AddHours(2), Start.AddHours(1), Start.AddHours(1).AddTicks(-1)));
        Assert.False(SessionBookingAvailability.IsOpen(SessionStatus.Running, Start.AddHours(-1), Start.AddHours(2), Start.AddHours(1), Start.AddHours(1)));
        Assert.False(SessionBookingAvailability.IsOpen(SessionStatus.Running, Start.AddHours(-1), Start.AddHours(2), Start.AddHours(1), Start.AddHours(1).AddTicks(1)));
    }

    [Fact]
    public void EarlierLastBookingTimeAndClosedStatusAlsoStopBooking()
    {
        Assert.True(SessionBookingAvailability.IsOpen(SessionStatus.Running, Start.AddHours(-1), Start.AddMinutes(30), Start.AddHours(1), Start.AddMinutes(30).AddTicks(-1)));
        Assert.False(SessionBookingAvailability.IsOpen(SessionStatus.Running, Start.AddHours(-1), Start.AddMinutes(30), Start.AddHours(1), Start.AddMinutes(30)));
        Assert.False(SessionBookingAvailability.IsOpen(SessionStatus.Closed, Start.AddHours(-1), Start.AddHours(1), Start.AddHours(1), Start));
    }
}

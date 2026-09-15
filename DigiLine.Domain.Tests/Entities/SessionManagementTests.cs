using DigiLine.Domain.Common;
using DigiLine.Domain.Entities;

namespace DigiLine.Domain.Tests.Entities;

public class SessionManagementTests
{
    [Theory]
    [InlineData(SessionStatus.Scheduled)]
    [InlineData(SessionStatus.BookingOpen)]
    [InlineData(SessionStatus.Running)]
    [InlineData(SessionStatus.Paused)]
    [InlineData(SessionStatus.Closed)]
    public void NoTokens_AllowsEditAndDeleteWithoutChangingLifecycle(SessionStatus status)
    {
        var session = NewSession(status);
        var stopped = session.BookingStoppedAt;
        var completed = session.CompletedAt;
        session.Reschedule(" Updated ", session.BookingOpenTime, session.StartTime, session.EndTime.AddHours(1), 20);
        session.EnsureCanDelete();
        Assert.Equal("Updated", session.Name);
        Assert.Equal(status, session.Status);
        Assert.Equal(stopped, session.BookingStoppedAt);
        Assert.Equal(completed, session.CompletedAt);
        Assert.Equal(20, session.AverageConsultationMinutes);
    }

    [Theory]
    [InlineData(SessionStatus.Scheduled)]
    [InlineData(SessionStatus.BookingOpen)]
    [InlineData(SessionStatus.Running)]
    [InlineData(SessionStatus.Paused)]
    [InlineData(SessionStatus.Closed)]
    public void AnyToken_BlocksEditAndDelete(SessionStatus status)
    {
        var session = NewSession(status);
        session.QueueTokens.Add(new QueueToken(session.Id, 1, "Patient", null));
        Assert.Throws<InvalidOperationException>(() => session.Reschedule("Updated", session.BookingOpenTime, session.StartTime, session.EndTime, 20));
        Assert.Throws<InvalidOperationException>(session.EnsureCanDelete);
        Assert.Equal("Original", session.Name);
    }

    [Theory]
    [InlineData(SessionStatus.Running)]
    [InlineData(SessionStatus.Paused)]
    [InlineData(SessionStatus.Closed)]
    public void StartedSession_CannotMoveStartOrBookingOpening(SessionStatus status)
    {
        var session = NewSession(status);
        Assert.Throws<InvalidOperationException>(() => session.Reschedule("Updated", session.BookingOpenTime, DateTime.UtcNow.AddHours(1), session.EndTime.AddHours(2), 10));
        Assert.Throws<InvalidOperationException>(() => session.Reschedule("Updated", session.BookingOpenTime.AddMinutes(-1), session.StartTime, session.EndTime, 10));
        Assert.Equal(status, session.Status);
    }

    [Fact]
    public void FutureSession_CanRescheduleButNotToANewPastStart()
    {
        var session = NewSession(SessionStatus.Scheduled);
        var start = DateTime.UtcNow.AddHours(5);
        session.Reschedule("Updated", start.AddHours(-1), start, start.AddHours(1), 10);
        Assert.Equal(start, session.StartTime);
        Assert.Throws<InvalidOperationException>(() => session.Reschedule("Invalid", start.AddDays(-1), DateTime.UtcNow.AddMinutes(-1), start, 10));
    }

    [Fact]
    public void EarlierCutoffAndManualStopArePreserved()
    {
        var start = DateTime.UtcNow.AddHours(-1);
        var cutoff = start.AddHours(2);
        var session = new Session(Guid.NewGuid(), Guid.NewGuid(), "Original", start.AddHours(-1), start, cutoff.AddHours(1), cutoff, 10);
        session.OpenBooking(); session.Start(); session.StopBooking(DateTime.UtcNow);
        session.Reschedule("Updated", session.BookingOpenTime, start, session.EndTime.AddHours(1), 10);
        Assert.Equal(cutoff, session.LastBookingTime);
        Assert.False(session.IsBookingOpen(DateTime.UtcNow));
        Assert.Throws<InvalidOperationException>(() => session.Reschedule("Invalid", session.BookingOpenTime, start, cutoff.AddMinutes(-1), 10));
        Assert.Throws<InvalidOperationException>(() => session.Reschedule("Invalid", session.BookingOpenTime, start, start, 10));
    }

    private static Session NewSession(SessionStatus status)
    {
        var start = DateTime.UtcNow.AddHours(status == SessionStatus.Scheduled ? 2 : -1);
        var session = new Session(Guid.NewGuid(), Guid.NewGuid(), "Original", start.AddHours(-1), start, start.AddHours(3), start.AddHours(3), 10);
        if (status != SessionStatus.Scheduled) session.OpenBooking();
        if (status is SessionStatus.Running or SessionStatus.Paused or SessionStatus.Closed) session.Start();
        if (status == SessionStatus.Paused) session.Pause();
        if (status == SessionStatus.Closed) { session.StopBooking(DateTime.UtcNow); session.CompleteSession(DateTime.UtcNow); }
        return session;
    }
}

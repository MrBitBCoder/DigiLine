using DigiLine.Application.Clinics;
using DigiLine.Domain.Common;

namespace DigiLine.Application.Tests;

public class GetPublicClinicServiceTests
{
    private static readonly DateTime Now = new(2026, 8, 19, 6, 0, 0, DateTimeKind.Utc);

    [Fact]
    public async Task GetAsync_ReturnsOnlyPublicSessionStateAndBackendCounts()
    {
        var repository = new StubRepository(new PublicClinicRecord(
            Guid.NewGuid(), "Public Clinic", "public-clinic",
            [new PublicSessionRecord(
                Guid.NewGuid(), "Morning", Now.AddHours(-1), Now.AddMinutes(-30),
                Now.AddHours(3), Now.AddHours(2), SessionStatus.Running,
                [
                    new(4, QueueTokenStatus.Waiting, Now.AddMinutes(-20), null),
                    new(5, QueueTokenStatus.Late, Now.AddMinutes(-15), null),
                    new(3, QueueTokenStatus.Called, Now.AddMinutes(-30), Now.AddMinutes(-5)),
                    new(2, QueueTokenStatus.Completed, Now.AddMinutes(-40), Now.AddMinutes(-20))
                ])]));
        var service = new GetPublicClinicService(repository, new TestClock(Now));

        var result = await service.GetAsync(" public-clinic ");

        Assert.NotNull(result);
        var session = Assert.Single(result.Sessions);
        Assert.True(session.IsBookingOpen);
        Assert.Equal(3, session.CurrentTokenNumber);
        Assert.Equal(2, session.ApproximateQueueSize);
        Assert.Equal("public-clinic", repository.Slug);
        Assert.Equal(Now.Date, repository.SessionsEndingAfter);
    }

    [Fact]
    public async Task GetAsync_ShowsLiveSessionsIndependentlyOfBookingWindow()
    {
        PublicSessionRecord Session(SessionStatus status, DateTime bookingOpen, DateTime lastBooking) =>
            new(Guid.NewGuid(), status.ToString(), bookingOpen, Now.AddHours(-1), Now.AddHours(2),
                lastBooking, status, []);
        var repository = new StubRepository(new PublicClinicRecord(Guid.NewGuid(), "Clinic", "clinic",
        [
            Session(SessionStatus.Running, Now.AddHours(-1), Now.AddHours(1)),
            Session(SessionStatus.Scheduled, Now.AddHours(-1), Now.AddHours(1)),
            Session(SessionStatus.BookingOpen, Now.AddHours(-1), Now.AddHours(1)),
            Session(SessionStatus.Paused, Now.AddHours(-1), Now.AddHours(1)),
            Session(SessionStatus.Running, Now.AddMinutes(1), Now.AddHours(1)),
            Session(SessionStatus.Running, Now.AddHours(-1), Now)
        ]));

        var result = await new GetPublicClinicService(repository, new TestClock(Now)).GetAsync("clinic");

        Assert.NotNull(result);
        Assert.Equal(new[] { SessionStatus.Running, SessionStatus.BookingOpen, SessionStatus.Paused, SessionStatus.Running, SessionStatus.Running },
            result.Sessions.Select(session => session.Status));
    }

    [Theory]
    [InlineData(SessionStatus.Running)]
    [InlineData(SessionStatus.Paused)]
    public async Task StoppedBooking_PreservesLiveSessionAndCurrentToken(SessionStatus status)
    {
        var record = new PublicSessionRecord(Guid.NewGuid(), "Live", Now.AddHours(-2), Now.AddHours(-1),
            Now.AddHours(2), Now.AddHours(1), status,
            [new(7, QueueTokenStatus.InConsultation, Now.AddMinutes(-20), Now.AddMinutes(-5))], Now.AddMinutes(-1));
        var repository = new StubRepository(new PublicClinicRecord(Guid.NewGuid(), "Clinic", "clinic", [record]));
        var result = await new GetPublicClinicService(repository, new TestClock(Now)).GetAsync("clinic");
        var session = Assert.Single(result!.Sessions);
        Assert.False(session.IsBookingOpen);
        Assert.Equal(status, session.Status);
        Assert.Equal(7, session.CurrentTokenNumber);
    }

    [Theory]
    [InlineData(SessionStatus.Running)]
    [InlineData(SessionStatus.Paused)]
    public async Task PastScheduledEnd_RemainsLiveWithoutAcceptingBookings(SessionStatus status)
    {
        var record = new PublicSessionRecord(Guid.NewGuid(), "Overrun", Now.AddDays(-2), Now.AddDays(-1),
            Now.AddHours(-12), Now.AddHours(-12), status, []);
        var result = await new GetPublicClinicService(new StubRepository(new PublicClinicRecord(
            Guid.NewGuid(), "Clinic", "clinic", [record])), new TestClock(Now)).GetAsync("clinic");
        Assert.False(Assert.Single(result!.Sessions).IsBookingOpen);
    }

    [Fact]
    public async Task CompletedSession_IsStillExcluded()
    {
        var record = new PublicSessionRecord(Guid.NewGuid(), "Completed", Now.AddHours(-2), Now.AddHours(-1),
            Now.AddHours(2), Now.AddHours(1), SessionStatus.Closed, [], Now);
        var result = await new GetPublicClinicService(new StubRepository(new PublicClinicRecord(
            Guid.NewGuid(), "Clinic", "clinic", [record])), new TestClock(Now)).GetAsync("clinic");
        Assert.Empty(result!.Sessions);
    }

    [Fact]
    public async Task GetAsync_WhenSlugDoesNotExist_ReturnsNull()
    {
        var result = await new GetPublicClinicService(
            new StubRepository(null), new TestClock(Now)).GetAsync("missing");
        Assert.Null(result);
    }

    [Fact]
    public void PublicResponseContract_DoesNotExposePatientOrStaffData()
    {
        var propertyNames = typeof(PublicSessionSummary).GetProperties()
            .Select(property => property.Name).ToList();
        Assert.DoesNotContain(propertyNames, name =>
            name.Contains("Patient", StringComparison.OrdinalIgnoreCase)
            || name.Contains("Phone", StringComparison.OrdinalIgnoreCase)
            || name.Contains("Doctor", StringComparison.OrdinalIgnoreCase)
            || name.Contains("TokenId", StringComparison.OrdinalIgnoreCase));
    }

    private sealed class TestClock(DateTime now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => new(now);
    }

    private sealed class StubRepository(PublicClinicRecord? clinic)
        : IPublicClinicRepository
    {
        public string? Slug { get; private set; }
        public DateTime SessionsEndingAfter { get; private set; }
        public Task<PublicClinicRecord?> GetBySlugAsync(string slug, DateTime sessionsEndingAfter, CancellationToken cancellationToken = default) { Slug = slug; SessionsEndingAfter = sessionsEndingAfter; return Task.FromResult(clinic?.Slug == slug ? clinic : null); }
    }
}

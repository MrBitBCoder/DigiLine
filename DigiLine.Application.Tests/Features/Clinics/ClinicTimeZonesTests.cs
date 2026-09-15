using DigiLine.Application.Clinics;

namespace DigiLine.Application.Tests;

public sealed class ClinicTimeZonesTests
{
    [Fact]
    public void TodayRange_UsesClinicMidnightInsteadOfUtcMidnight()
    {
        var range = ClinicTimeZones.TodayRange("Asia/Kolkata", new DateTime(2026, 9, 14, 20, 0, 0, DateTimeKind.Utc));
        Assert.Equal(new DateTime(2026, 9, 15), range.LocalDate);
        Assert.Equal(new DateTime(2026, 9, 14, 18, 30, 0, DateTimeKind.Utc), range.FromUtc);
        Assert.Equal(new DateTime(2026, 9, 15, 18, 30, 0, DateTimeKind.Utc), range.ToUtc);
    }

    [Fact]
    public void TodayRange_HandlesDaylightSavingDayLength()
    {
        var range = ClinicTimeZones.TodayRange("America/New_York", new DateTime(2026, 3, 8, 12, 0, 0, DateTimeKind.Utc));
        Assert.Equal(TimeSpan.FromHours(23), range.ToUtc - range.FromUtc);
    }
}

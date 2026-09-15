namespace DigiLine.Application.Clinics;

public interface IClinicTimeZoneResolver
{
    Task<string> GetTimeZoneIdAsync(Guid clinicId, CancellationToken cancellationToken = default);
}

public static class ClinicTimeZones
{
    public static (DateTime FromUtc, DateTime ToUtc, DateTime LocalDate) TodayRange(string zoneId, DateTime utcNow)
    {
        var zone = TimeZoneInfo.FindSystemTimeZoneById(zoneId);
        var localToday = TimeZoneInfo.ConvertTimeFromUtc(utcNow, zone).Date;
        var nextDay = localToday.AddDays(1);
        return (TimeZoneInfo.ConvertTimeToUtc(localToday, zone),
            TimeZoneInfo.ConvertTimeToUtc(nextDay, zone), localToday);
    }
}

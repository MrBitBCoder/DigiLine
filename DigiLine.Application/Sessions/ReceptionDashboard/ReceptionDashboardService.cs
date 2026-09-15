using DigiLine.Domain.Common;
using DigiLine.Domain.Entities;
using DigiLine.Application.Clinics;

namespace DigiLine.Application.Sessions;

public sealed record ReceptionDashboard(string ClinicName, DateTime Date, IReadOnlyList<DoctorSessionDetails> Sessions, string TimeZoneId = "Asia/Kolkata");

public interface IGetReceptionDashboardService
{
    Task<ReceptionDashboard> GetTodayAsync(Guid clinicId, CancellationToken cancellationToken = default);
    Task<DoctorSessionDetails?> GetSessionAsync(Guid sessionId, Guid clinicId, CancellationToken cancellationToken = default);
}

public sealed class GetReceptionDashboardService : IGetReceptionDashboardService
{
    private readonly ISessionRepository _sessions;
    private readonly TimeProvider _clock;
    private readonly IClinicTimeZoneResolver? _timeZones;

    public GetReceptionDashboardService(ISessionRepository sessions, TimeProvider? clock = null, IClinicTimeZoneResolver? timeZones = null)
    {
        _sessions = sessions;
        _clock = clock ?? TimeProvider.System;
        _timeZones = timeZones;
    }

    public async Task<ReceptionDashboard> GetTodayAsync(Guid clinicId, CancellationToken cancellationToken = default)
    {
        var now = _clock.GetUtcNow().UtcDateTime;
        var zoneId = _timeZones is null ? "Asia/Kolkata" : await _timeZones.GetTimeZoneIdAsync(clinicId, cancellationToken);
        var range = ClinicTimeZones.TodayRange(zoneId, now);
        var records = await _sessions.ListForClinicAsync(clinicId, range.FromUtc, range.ToUtc, cancellationToken);
        return new ReceptionDashboard(records.FirstOrDefault()?.ClinicName ?? string.Empty, range.LocalDate,
            records.Select(record => Map(record, now)).ToList(), zoneId);
    }

    public async Task<DoctorSessionDetails?> GetSessionAsync(Guid sessionId, Guid clinicId, CancellationToken cancellationToken = default)
    {
        var record = await _sessions.GetForClinicAsync(sessionId, clinicId, cancellationToken);
        return record is null ? null : Map(record, _clock.GetUtcNow().UtcDateTime);
    }

    private static DoctorSessionDetails Map(DoctorSessionRecord record, DateTime now)
    {
        var current = record.Queue.Where(token => token.Status is QueueTokenStatus.Called or QueueTokenStatus.InConsultation)
            .OrderByDescending(token => token.CalledAt ?? token.CreatedAt).FirstOrDefault();
        var summary = new DoctorSessionSummary(record.Id, record.ClinicId, record.ClinicName,
            record.DoctorId, record.DoctorName, record.Name, record.BookingOpenTime, record.StartTime,
            record.EndTime, record.LastBookingTime, record.AverageConsultationMinutes, record.Status,
            SessionBookingAvailability.IsOpen(record.Status, record.BookingOpenTime, record.LastBookingTime, record.EndTime, now),
            new DoctorQueueSummary(current,
                record.Queue.Count(token => token.Status == QueueTokenStatus.Waiting),
                record.Queue.Count(token => token.Status == QueueTokenStatus.Late), record.Queue.Count,
                record.Queue.Count(token => token.Status == QueueTokenStatus.Completed),
                record.Queue.Count(token => token.Status is QueueTokenStatus.NoShow or QueueTokenStatus.Skipped)), record.TimeZoneId);
        return new DoctorSessionDetails(summary, record.Queue.OrderBy(token => token.QueueOrder).ThenBy(token => token.TokenNumber).ToList());
    }
}

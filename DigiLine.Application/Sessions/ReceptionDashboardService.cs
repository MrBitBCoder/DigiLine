using DigiLine.Domain.Common;

namespace DigiLine.Application.Sessions;

public sealed record ReceptionDashboard(string ClinicName, DateTime Date, IReadOnlyList<DoctorSessionDetails> Sessions);

public interface IGetReceptionDashboardService
{
    Task<ReceptionDashboard> GetTodayAsync(Guid clinicId, CancellationToken cancellationToken = default);
    Task<DoctorSessionDetails?> GetSessionAsync(Guid sessionId, Guid clinicId, CancellationToken cancellationToken = default);
}

public sealed class GetReceptionDashboardService : IGetReceptionDashboardService
{
    private readonly ISessionRepository _sessions;
    private readonly TimeProvider _clock;

    public GetReceptionDashboardService(ISessionRepository sessions, TimeProvider? clock = null)
    {
        _sessions = sessions;
        _clock = clock ?? TimeProvider.System;
    }

    public async Task<ReceptionDashboard> GetTodayAsync(Guid clinicId, CancellationToken cancellationToken = default)
    {
        var now = _clock.GetUtcNow().UtcDateTime;
        var start = now.Date;
        var records = await _sessions.ListForClinicAsync(clinicId, start, start.AddDays(1), cancellationToken);
        return new ReceptionDashboard(records.FirstOrDefault()?.ClinicName ?? string.Empty, start,
            records.Select(record => Map(record, now)).ToList());
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
            record.Status != SessionStatus.Closed && now >= record.BookingOpenTime && now <= record.LastBookingTime,
            new DoctorQueueSummary(current,
                record.Queue.Count(token => token.Status == QueueTokenStatus.Waiting),
                record.Queue.Count(token => token.Status == QueueTokenStatus.Late), record.Queue.Count));
        return new DoctorSessionDetails(summary, record.Queue.OrderBy(token => token.TokenNumber).ToList());
    }
}

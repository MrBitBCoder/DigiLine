using DigiLine.Domain.Common;

namespace DigiLine.Application.Sessions;

public sealed class GetDoctorSessionsService : IGetDoctorSessionsService
{
    private readonly ISessionRepository _sessions;
    private readonly TimeProvider _clock;

    public GetDoctorSessionsService(ISessionRepository sessions, TimeProvider? clock = null)
    {
        _sessions = sessions;
        _clock = clock ?? TimeProvider.System;
    }

    public async Task<IReadOnlyList<DoctorSessionSummary>> ListAsync(Guid clinicId, Guid doctorId, CancellationToken cancellationToken = default)
    {
        var now = _clock.GetUtcNow().UtcDateTime;
        var sessions = await _sessions.ListForDoctorAsync(clinicId, doctorId, cancellationToken);
        return sessions.Select(session => MapSummary(session, now)).ToList();
    }

    public async Task<DoctorSessionDetails?> GetAsync(Guid sessionId, Guid clinicId, Guid doctorId, CancellationToken cancellationToken = default)
    {
        var session = await _sessions.GetForDoctorAsync(sessionId, clinicId, doctorId, cancellationToken);
        return session is null ? null : MapDetails(session, _clock.GetUtcNow().UtcDateTime);
    }

    public async Task<DoctorSessionDetails?> GetActiveOrUpcomingAsync(Guid clinicId, Guid doctorId, CancellationToken cancellationToken = default)
    {
        var now = _clock.GetUtcNow().UtcDateTime;
        var sessions = await _sessions.ListForDoctorAsync(clinicId, doctorId, cancellationToken);
        var selected = sessions
            .Where(session => session.Status is SessionStatus.Running or SessionStatus.Paused)
            .OrderBy(session => session.StartTime)
            .FirstOrDefault()
            ?? sessions
                .Where(session => session.Status != SessionStatus.Closed && session.EndTime >= now)
                .OrderBy(session => session.StartTime)
                .FirstOrDefault();
        return selected is null ? null : MapDetails(selected, now);
    }

    private static DoctorSessionDetails MapDetails(DoctorSessionRecord record, DateTime now) =>
        new(MapSummary(record, now), record.Queue.OrderBy(token => token.TokenNumber).ToList());

    private static DoctorSessionSummary MapSummary(DoctorSessionRecord record, DateTime now)
    {
        var current = record.Queue
            .Where(token => token.Status is QueueTokenStatus.Called or QueueTokenStatus.InConsultation)
            .OrderByDescending(token => token.CalledAt ?? token.CreatedAt)
            .FirstOrDefault();
        return new DoctorSessionSummary(
            record.Id, record.ClinicId, record.ClinicName, record.DoctorId, record.DoctorName,
            record.Name, record.BookingOpenTime, record.StartTime, record.EndTime,
            record.LastBookingTime, record.AverageConsultationMinutes, record.Status,
            record.Status != SessionStatus.Closed && now >= record.BookingOpenTime && now <= record.LastBookingTime,
            new DoctorQueueSummary(
                current,
                record.Queue.Count(token => token.Status == QueueTokenStatus.Waiting),
                record.Queue.Count(token => token.Status == QueueTokenStatus.Late),
                record.Queue.Count));
    }
}

using DigiLine.Domain.Common;

namespace DigiLine.Application.Clinics;

public sealed class GetPublicClinicService : IGetPublicClinicService
{
    private readonly IPublicClinicRepository _clinics;
    private readonly TimeProvider _clock;

    public GetPublicClinicService(
        IPublicClinicRepository clinics,
        TimeProvider? clock = null)
    {
        _clinics = clinics;
        _clock = clock ?? TimeProvider.System;
    }

    public async Task<PublicClinicDetails?> GetAsync(
        string clinicSlug,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(clinicSlug)) return null;

        var now = _clock.GetUtcNow().UtcDateTime;
        var clinic = await _clinics.GetBySlugAsync(
            clinicSlug.Trim(), now.Date, cancellationToken);
        if (clinic is null) return null;

        var sessions = clinic.Sessions
            .OrderBy(session => session.StartTime)
            .Select(session => new PublicSessionSummary(
                session.Id,
                session.Name,
                session.BookingOpenTime,
                session.StartTime,
                session.EndTime,
                session.LastBookingTime,
                session.Status,
                session.Status != SessionStatus.Closed
                    && now >= session.BookingOpenTime
                    && now <= session.LastBookingTime,
                session.Queue
                    .Where(token => token.Status is QueueTokenStatus.Called
                        or QueueTokenStatus.InConsultation)
                    .OrderByDescending(token => token.CalledAt ?? token.CreatedAt)
                    .Select(token => (int?)token.TokenNumber)
                    .FirstOrDefault(),
                session.Queue.Count(token => token.Status is
                    QueueTokenStatus.Waiting or QueueTokenStatus.Late)))
            .ToList();

        return new PublicClinicDetails(clinic.Name, clinic.Slug, sessions);
    }
}

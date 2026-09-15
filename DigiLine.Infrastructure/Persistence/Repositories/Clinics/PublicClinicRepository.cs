using DigiLine.Application.Clinics;
using DigiLine.Domain.Common;
using Microsoft.EntityFrameworkCore;

namespace DigiLine.Infrastructure.Persistence.Repositories;

public sealed class PublicClinicRepository : IPublicClinicRepository
{
    private readonly DigiLineDbContext _dbContext;

    public PublicClinicRepository(DigiLineDbContext dbContext) =>
        _dbContext = dbContext;

    public async Task<PublicClinicRecord?> GetBySlugAsync(
        string slug,
        DateTime sessionsEndingAfter,
        CancellationToken cancellationToken = default)
    {
        return await _dbContext.Clinics
            .AsNoTracking()
            .Where(clinic => clinic.Slug == slug)
            .Select(clinic => new PublicClinicRecord(
                clinic.Id,
                clinic.Name,
                clinic.Slug,
                _dbContext.Sessions
                    .Where(session => session.ClinicId == clinic.Id
                        && (session.EndTime >= sessionsEndingAfter
                            || session.Status == SessionStatus.Running || session.Status == SessionStatus.Paused))
                    .Select(session => new PublicSessionRecord(
                        session.Id,
                        session.Name,
                        session.BookingOpenTime,
                        session.StartTime,
                        session.EndTime,
                        session.LastBookingTime,
                        session.Status,
                        session.QueueTokens
                            .Select(token => new PublicQueueTokenRecord(
                                token.TokenNumber,
                                token.Status,
                                token.CreatedAt,
                                token.CalledAt))
                            .ToList(), session.BookingStoppedAt))
                    .ToList(), clinic.TimeZoneId))
            .SingleOrDefaultAsync(cancellationToken);
    }
}

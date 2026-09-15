using DigiLine.Application.Sessions;
using DigiLine.Domain.Entities;
using DigiLine.Application.Queue;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DigiLine.Infrastructure.Persistence.Repositories
{

    public class SessionRepository : ISessionRepository
    {
        private readonly DigiLineDbContext _dbContext;

        public SessionRepository(DigiLineDbContext dbContext)
        {
            _dbContext = dbContext;
        }

        public async Task AddAsync(
            Session session,
            CancellationToken cancellationToken = default)
        {
            if (_dbContext.Database.CurrentTransaction is not null)
            {
                await _dbContext.Clinics.FromSqlInterpolated($"SELECT * FROM Clinics WITH (UPDLOCK, HOLDLOCK) WHERE Id = {session.ClinicId}").SingleAsync(cancellationToken);
                if (!await _dbContext.Staff.AnyAsync(x => x.Id == session.DoctorId && x.ClinicId == session.ClinicId && x.IsActive && !x.InvitationPending && x.Roles.Any(r => r.Role == DigiLine.Domain.Common.StaffRole.Doctor), cancellationToken))
                    throw new InvalidOperationException("The doctor account is no longer active.");
            }
            await _dbContext.Sessions.AddAsync(
                session,
                cancellationToken);
        }

        public async Task SaveChangesAsync(
            CancellationToken cancellationToken = default)
        {
            await _dbContext.SaveChangesAsync(cancellationToken);
        }

        public async Task<Session?> GetByIdAsync(
            Guid sessionId,
            CancellationToken cancellationToken = default)
        {
            var query = _dbContext.Database.CurrentTransaction is null ? _dbContext.Sessions :
                _dbContext.Sessions.FromSqlInterpolated($"SELECT * FROM Sessions WITH (UPDLOCK, HOLDLOCK) WHERE Id = {sessionId}");
            return await query.FirstOrDefaultAsync(
                    x => x.Id == sessionId,
                    cancellationToken);
        }

        public async Task<Session?> GetForDoctorForUpdateAsync(Guid sessionId, Guid clinicId, Guid doctorId,
            CancellationToken cancellationToken = default)
        {
            var session = await GetByIdAsync(sessionId, cancellationToken);
            if (session is null || session.ClinicId != clinicId || session.DoctorId != doctorId) return null;
            await _dbContext.Entry(session).Collection(x => x.QueueTokens).LoadAsync(cancellationToken);
            return session;
        }

        public async Task<IReadOnlyList<QueueToken>> GetUnfinishedTokensForUpdateAsync(Guid sessionId, CancellationToken cancellationToken = default) =>
            await _dbContext.QueueTokens.Where(x => x.SessionId == sessionId && x.Status != DigiLine.Domain.Common.QueueTokenStatus.Completed && x.Status != DigiLine.Domain.Common.QueueTokenStatus.Cancelled && x.Status != DigiLine.Domain.Common.QueueTokenStatus.NoShow).ToListAsync(cancellationToken);

        public Task DeleteAsync(Session session, CancellationToken cancellationToken = default)
        {
            _dbContext.Sessions.Remove(session);
            return Task.CompletedTask;
        }

        public async Task<IReadOnlyList<DoctorSessionRecord>> ListForDoctorAsync(
            Guid clinicId, Guid doctorId, CancellationToken cancellationToken = default)
        {
            var sessions = await DoctorSessions(clinicId, doctorId)
                .OrderByDescending(session => session.StartTime)
                .ToListAsync(cancellationToken);
            return sessions.Select(MapDoctorSession).ToList();
        }

        public async Task<DoctorSessionRecord?> GetForDoctorAsync(
            Guid sessionId, Guid clinicId, Guid doctorId,
            CancellationToken cancellationToken = default)
        {
            var session = await DoctorSessions(clinicId, doctorId)
                .SingleOrDefaultAsync(session => session.Id == sessionId, cancellationToken);
            return session is null ? null : MapDoctorSession(session);
        }

        public async Task<IReadOnlyList<DoctorSessionRecord>> ListForClinicAsync(
            Guid clinicId, DateTime from, DateTime to, CancellationToken cancellationToken = default)
        {
            var sessions = await ClinicSessions(clinicId)
                .Where(session => session.StartTime >= from && session.StartTime < to)
                .OrderBy(session => session.StartTime)
                .ToListAsync(cancellationToken);
            return sessions.Select(MapDoctorSession).ToList();
        }

        public async Task<DoctorSessionRecord?> GetForClinicAsync(
            Guid sessionId, Guid clinicId, CancellationToken cancellationToken = default)
        {
            var session = await ClinicSessions(clinicId)
                .SingleOrDefaultAsync(session => session.Id == sessionId, cancellationToken);
            return session is null ? null : MapDoctorSession(session);
        }

        private IQueryable<Session> DoctorSessions(Guid clinicId, Guid doctorId) =>
            ClinicSessions(clinicId).Where(session => session.DoctorId == doctorId);

        private IQueryable<Session> ClinicSessions(Guid clinicId) =>
            _dbContext.Sessions.AsNoTracking()
                .Where(session => session.ClinicId == clinicId)
                .Include(session => session.Clinic)
                .Include(session => session.Doctor)
                .Include(session => session.QueueTokens)
                    .ThenInclude(token => token.PatientProfile)
                    .ThenInclude(profile => profile!.PhoneAccount);

        private static DoctorSessionRecord MapDoctorSession(Session session) =>
            new(
                session.Id, session.ClinicId, session.Clinic.Name,
                session.DoctorId, session.Doctor.Name, session.Name,
                session.BookingOpenTime, session.StartTime, session.EndTime,
                session.LastBookingTime, session.AverageConsultationMinutes, session.Status,
                session.QueueTokens.OrderBy(token => token.QueueOrder)
                    .Select(token => new QueueTokenListItem(
                        token.Id, token.TokenNumber, token.PatientProfileId,
                        token.WalkInName, token.PatientProfile?.PhoneAccount.PhoneNumber ?? token.PhoneNumber, token.Status,
                        token.CreatedAt, token.CalledAt, token.CompletedAt,
                        token.PatientProfile?.Name ?? token.WalkInName, token.PatientProfile?.Age ?? token.WalkInAge,
                        token.LateArrivedAt, token.QueueOrder))
                    .ToList(), session.Clinic.TimeZoneId);
    }
}

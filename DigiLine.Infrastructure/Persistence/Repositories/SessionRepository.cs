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
            return await _dbContext.Sessions
                .FirstOrDefaultAsync(
                    x => x.Id == sessionId,
                    cancellationToken);
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
                .Include(session => session.QueueTokens);

        private static DoctorSessionRecord MapDoctorSession(Session session) =>
            new(
                session.Id, session.ClinicId, session.Clinic.Name,
                session.DoctorId, session.Doctor.Name, session.Name,
                session.BookingOpenTime, session.StartTime, session.EndTime,
                session.LastBookingTime, session.AverageConsultationMinutes, session.Status,
                session.QueueTokens.OrderBy(token => token.TokenNumber)
                    .Select(token => new QueueTokenListItem(
                        token.Id, token.TokenNumber, token.PatientProfileId,
                        token.WalkInName, token.PhoneNumber, token.Status,
                        token.CreatedAt, token.CalledAt, token.CompletedAt))
                    .ToList());
    }
}

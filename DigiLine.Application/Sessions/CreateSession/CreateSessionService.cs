using DigiLine.Domain.Entities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DigiLine.Application.Sessions
{
    public class CreateSessionService : ICreateSessionService
    {
        private readonly ISessionRepository _sessionRepository;

        public CreateSessionService(ISessionRepository sessionRepository)
        {
            _sessionRepository = sessionRepository;
        }

        public async Task<Guid> CreateAsync(
            CreateSessionRequest request,
            CancellationToken cancellationToken = default)
        {
            var session = new Session(
                request.ClinicId,
                request.DoctorId,
                request.Name,
                request.BookingOpenTime,
                request.StartTime,
                request.EndTime,
                request.LastBookingTime,
                request.AverageConsultationMinutes);

            await _sessionRepository.AddAsync(
                session,
                cancellationToken);

            await _sessionRepository.SaveChangesAsync(
                cancellationToken);

            return session.Id;
        }
    }
}

using DigiLine.Application.Patients;
using DigiLine.Application.Sessions;
using DigiLine.Application.Transactions;
using DigiLine.Domain.Common;
using DigiLine.Domain.Entities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DigiLine.Application.Queue
{
    public class BookTokenService : IBookTokenService
    {
        private readonly ITokenNumberService _tokenNumberService;
        private readonly IQueueTokenRepository _queueTokenRepository;
        private readonly ISessionRepository _sessionRepository;
        private readonly IPatientProfileRepository _patientProfileRepository;
        private readonly ITransactionManager _transactionManager;


        public BookTokenService(
            ITokenNumberService tokenNumberService,
            IQueueTokenRepository queueTokenRepository,
            ISessionRepository sessionRepository,
            IPatientProfileRepository patientProfileRepository,
             ITransactionManager transactionManager)
        {
            _tokenNumberService = tokenNumberService;
            _queueTokenRepository = queueTokenRepository;
            _sessionRepository = sessionRepository;
            _patientProfileRepository = patientProfileRepository;
            _transactionManager = transactionManager;
        }

        public async Task<int> BookAsync(
            Guid sessionId,
            Guid patientProfileId,
            CancellationToken cancellationToken = default)
        {

            var session = await _sessionRepository.GetByIdAsync(
                        sessionId,
                        cancellationToken);

            if (session is null)
            {
                throw new InvalidOperationException(
                    "Session was not found.");
            }

            if (!session.IsBookingOpen(DateTime.UtcNow))
            {
                throw new InvalidOperationException(
                    "Booking is not currently open for this session.");
            }

            if (session.Status == SessionStatus.Closed)
            {
                throw new InvalidOperationException(
                    "Cannot book a token for a closed session.");
            }

            var patientProfile =
                        await _patientProfileRepository.GetByIdAsync(
                            patientProfileId,
                            cancellationToken);

            if (patientProfile is null)
            {
                throw new InvalidOperationException(
                    "Patient profile was not found.");
            }

            await using var transaction =
                await _transactionManager.BeginAsync(
                    cancellationToken);


            var tokenNumber =
                await _tokenNumberService.GetNextTokenNumberAsync(
                    sessionId,
                    cancellationToken);

            var queueToken = new QueueToken(
                sessionId,
                tokenNumber,
                patientProfileId);

            await _queueTokenRepository.AddAsync(
                queueToken,
                cancellationToken);

            await _queueTokenRepository.SaveChangesAsync(
                 cancellationToken);

            await transaction.CommitAsync(
                cancellationToken);

            return tokenNumber;
        }
    }
}

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
    public class CreateWalkInTokenService : ICreateWalkInTokenService
    {
        private readonly ITokenNumberService _tokenNumberService;
        private readonly IQueueTokenRepository _queueTokenRepository;
        private readonly ISessionRepository _sessionRepository;
        private readonly ITransactionManager _transactionManager;
        private readonly IQueueEventPublisher? _queueEventPublisher;

        public CreateWalkInTokenService(
            ITokenNumberService tokenNumberService,
            IQueueTokenRepository queueTokenRepository,
            ISessionRepository sessionRepository,
            ITransactionManager transactionManager,
            IQueueEventPublisher? queueEventPublisher = null)
        {
            _tokenNumberService = tokenNumberService;
            _queueTokenRepository = queueTokenRepository;
            _sessionRepository = sessionRepository;
            _transactionManager = transactionManager;
            _queueEventPublisher = queueEventPublisher;
        }

        public async Task<int> CreateAsync(
            Guid sessionId,
            string walkInName,
            string? phoneNumber,
            CancellationToken cancellationToken = default,
            int? age = null)
        {
            await using var transaction =
                await _transactionManager.BeginAsync(
                    cancellationToken);
        var session = await _sessionRepository.GetByIdAsync(
                sessionId,
                cancellationToken);

            if (session is null)
            {
                throw new InvalidOperationException(
                    "Session was not found.");
            }

            if (session.Status == SessionStatus.Closed)
            {
                throw new InvalidOperationException(
                    "Cannot create a token for a closed session.");
            }

            if (!session.IsBookingOpen(DateTime.UtcNow))
            {
                throw new InvalidOperationException(
                    "Booking is not currently open for this session.");
            }


            var tokenNumber =
                await _tokenNumberService.GetNextTokenNumberAsync(
                    sessionId,
                    cancellationToken);

            var queueToken = new QueueToken(
                sessionId,
                tokenNumber,
                walkInName,
                phoneNumber,
                age);

            queueToken.AssignQueueOrder(
                await _queueTokenRepository.AllocateNextQueueOrderAsync(sessionId, cancellationToken)
                    ?? throw new InvalidOperationException("Session was not found."));

            await _queueTokenRepository.AddAsync(
                queueToken,
                cancellationToken);

            await _queueTokenRepository.SaveChangesAsync(
                cancellationToken);

            await transaction.CommitAsync(
                cancellationToken);

            if (_queueEventPublisher is not null)
            {
                await _queueEventPublisher.PublishAsync(
                    new QueueChangedEvent(
                        QueueEventType.WalkInCreated,
                        sessionId,
                        queueToken.Id,
                        queueToken.TokenNumber,
                        queueToken.Status,
                        session.Status,
                        DateTime.UtcNow),
                    cancellationToken);
            }

            return tokenNumber;
        }
    }
}

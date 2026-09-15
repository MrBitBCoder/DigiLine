using DigiLine.Application.Sessions;
using DigiLine.Domain.Common;

namespace DigiLine.Application.Queue;

public sealed class GetQueuePositionService : IGetQueuePositionService
{
    private readonly ISessionRepository _sessionRepository;
    private readonly IQueueTokenRepository _queueTokenRepository;

    public GetQueuePositionService(
        ISessionRepository sessionRepository,
        IQueueTokenRepository queueTokenRepository)
    {
        _sessionRepository = sessionRepository;
        _queueTokenRepository = queueTokenRepository;
    }

    public async Task<QueuePositionResult> GetAsync(
        Guid sessionId,
        Guid tokenId,
        CancellationToken cancellationToken = default)
    {
        var session = await _sessionRepository.GetByIdAsync(
            sessionId,
            cancellationToken);

        if (session is null)
        {
            throw new InvalidOperationException("Session was not found.");
        }

        if (session.Status == SessionStatus.Closed)
        {
            throw new InvalidOperationException(
                "Queue position is not available for a closed session.");
        }

        var token = await _queueTokenRepository.GetByIdAsync(
            sessionId,
            tokenId,
            cancellationToken);

        if (token is null)
        {
            throw new InvalidOperationException("Queue token was not found.");
        }

        if (token.Status != QueueTokenStatus.Waiting
            && token.Status != QueueTokenStatus.Late)
        {
            throw new InvalidOperationException(
                "Queue position is only available for waiting or late tokens.");
        }

        var tokens = await _queueTokenRepository.GetBySessionOrderedAsync(
            sessionId,
            cancellationToken);

        var patientsAhead = tokens.Count(candidate =>
            candidate.Status is QueueTokenStatus.Waiting or QueueTokenStatus.Late
            && (candidate.QueueOrder < token.QueueOrder
                || (candidate.QueueOrder == token.QueueOrder
                    && candidate.TokenNumber < token.TokenNumber)));

        return new QueuePositionResult(
            token.Id,
            token.TokenNumber,
            token.Status,
            patientsAhead + 1,
            patientsAhead);
    }
}

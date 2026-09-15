using DigiLine.Application.Transactions;
using DigiLine.Application.Sessions;
using DigiLine.Domain.Common;

namespace DigiLine.Application.Queue;

public sealed class CompleteTokenService : ICompleteTokenService
{
    private readonly ITransactionManager? _transactionManager;
    private readonly ISessionRepository _sessionRepository;
    private readonly IQueueTokenRepository _queueTokenRepository;
    private readonly IQueueEventPublisher? _queueEventPublisher;

    public CompleteTokenService(
        ISessionRepository sessionRepository,
        IQueueTokenRepository queueTokenRepository,
        IQueueEventPublisher? queueEventPublisher = null, ITransactionManager? transactionManager = null)
    {
        _transactionManager = transactionManager;
        _sessionRepository = sessionRepository;
        _queueTokenRepository = queueTokenRepository;
        _queueEventPublisher = queueEventPublisher;
    }

    public async Task<QueueTokenListItem> CompleteAsync(
        Guid sessionId,
        Guid tokenId,
        CancellationToken cancellationToken = default)
    {
        await using var transaction = _transactionManager is null ? null : await _transactionManager.BeginAsync(cancellationToken);
        var session = await _sessionRepository.GetByIdAsync(
            sessionId,
            cancellationToken);

        if (session is null)
        {
            throw new InvalidOperationException("Session was not found.");
        }

        if (session.Status != SessionStatus.Running)
        {
            throw new InvalidOperationException(
                "Complete is only available for a running session.");
        }

        var token = await _queueTokenRepository.GetByIdAsync(
            sessionId,
            tokenId,
            cancellationToken);

        if (token is null)
        {
            throw new InvalidOperationException("Queue token was not found.");
        }

        token.Complete(DateTime.UtcNow);
        await _queueTokenRepository.SaveChangesAsync(cancellationToken);
        if (transaction is not null) await transaction.CommitAsync(cancellationToken);

        if (_queueEventPublisher is not null)
        {
            await _queueEventPublisher.PublishAsync(
                new QueueChangedEvent(
                    QueueEventType.TokenCompleted, sessionId, token.Id,
                    token.TokenNumber, token.Status, session.Status, DateTime.UtcNow),
                cancellationToken);
        }

        return new QueueTokenListItem(
            token.Id,
            token.TokenNumber,
            token.PatientProfileId,
            token.WalkInName,
            token.PhoneNumber,
            token.Status,
            token.CreatedAt,
            token.CalledAt,
            token.CompletedAt);
    }
}

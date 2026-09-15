using DigiLine.Application.Sessions;
using DigiLine.Application.Transactions;
using DigiLine.Domain.Common;

namespace DigiLine.Application.Queue;

public sealed class MarkTokenLateService : IMarkTokenLateService
{
    private readonly ISessionRepository _sessionRepository;
    private readonly IQueueTokenRepository _queueTokenRepository;
    private readonly IQueueEventPublisher? _queueEventPublisher;
    private readonly ITransactionManager? _transactionManager;

    public MarkTokenLateService(
        ISessionRepository sessionRepository,
        IQueueTokenRepository queueTokenRepository,
        IQueueEventPublisher? queueEventPublisher = null,
        ITransactionManager? transactionManager = null)
    {
        _sessionRepository = sessionRepository;
        _queueTokenRepository = queueTokenRepository;
        _queueEventPublisher = queueEventPublisher;
        _transactionManager = transactionManager;
    }

    public async Task<QueueTokenListItem> MarkLateAsync(
        Guid sessionId,
        Guid tokenId,
        CancellationToken cancellationToken = default)
    {
        await using var transaction = _transactionManager is null
            ? null
            : await _transactionManager.BeginAsync(cancellationToken);
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
                "Late arrival handling is only available for a running session.");
        }


        var token = await _queueTokenRepository.GetByIdForUpdateAsync(
            sessionId,
            tokenId,
            cancellationToken);

        if (token is null)
        {
            throw new InvalidOperationException("Queue token was not found.");
        }

        if (token.Status != QueueTokenStatus.Skipped)
            throw new InvalidOperationException("Only a skipped token can be marked late.");

        var queueOrder = await _queueTokenRepository.AllocateNextQueueOrderAsync(sessionId, cancellationToken)
            ?? throw new InvalidOperationException("Session was not found.");
        token.MarkLate(DateTime.UtcNow, queueOrder);
        await _queueTokenRepository.SaveChangesAsync(cancellationToken);
        if (transaction is not null) await transaction.CommitAsync(cancellationToken);

        if (_queueEventPublisher is not null)
        {
            await _queueEventPublisher.PublishAsync(
                new QueueChangedEvent(
                    QueueEventType.TokenMarkedLate, sessionId, token.Id,
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
            token.CompletedAt,
            LateArrivedAt: token.LateArrivedAt,
            QueueOrder: token.QueueOrder);
    }
}

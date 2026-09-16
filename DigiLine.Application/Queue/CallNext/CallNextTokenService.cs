using DigiLine.Application.Sessions;
using DigiLine.Application.Transactions;
using DigiLine.Domain.Common;

namespace DigiLine.Application.Queue;

public sealed class CallNextTokenService : ICallNextTokenService
{
    private readonly ISessionRepository _sessionRepository;
    private readonly IQueueTokenRepository _queueTokenRepository;
    private readonly ITransactionManager _transactionManager;
    private readonly IQueueEventPublisher? _queueEventPublisher;

    public CallNextTokenService(
        ISessionRepository sessionRepository,
        IQueueTokenRepository queueTokenRepository,
        ITransactionManager transactionManager,
        IQueueEventPublisher? queueEventPublisher = null)
    {
        _sessionRepository = sessionRepository;
        _queueTokenRepository = queueTokenRepository;
        _transactionManager = transactionManager;
        _queueEventPublisher = queueEventPublisher;
    }

    public async Task<QueueTokenListItem> CallAsync(
        Guid sessionId,
        CancellationToken cancellationToken = default)
    {
        await using var transaction = await _transactionManager.BeginAsync(
            cancellationToken);
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
                "Call Next is only available for a running session.");
        }

        if (await _queueTokenRepository.GetCurrentForUpdateAsync(sessionId, cancellationToken) is not null)
            throw new InvalidOperationException(
                "Complete or skip the current patient before calling next.");

        var token = await _queueTokenRepository.GetNextWaitingForUpdateAsync(
            sessionId,
            cancellationToken);

        if (token is null)
        {
            throw new InvalidOperationException(
                "There are no callable tokens in this session.");
        }

        token.Call(DateTime.UtcNow);

        await _queueTokenRepository.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        if (_queueEventPublisher is not null)
        {
            await _queueEventPublisher.PublishAsync(
                new QueueChangedEvent(
                    QueueEventType.TokenCalled,
                    sessionId,
                    token.Id,
                    token.TokenNumber,
                    token.Status,
                    session.Status,
                    DateTime.UtcNow),
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

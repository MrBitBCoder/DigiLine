using DigiLine.Application.Sessions;
using DigiLine.Application.Transactions;
using DigiLine.Domain.Common;
using DigiLine.Domain.Entities;

namespace DigiLine.Application.Queue;

public sealed class CompleteAndCallNextTokenService(
    ISessionRepository sessions,
    IQueueTokenRepository tokens,
    ITransactionManager transactions,
    IQueueEventPublisher? events = null) : ICompleteAndCallNextTokenService
{
    public async Task<CompleteAndCallNextResult> CompleteAndCallNextAsync(
        Guid sessionId,
        Guid tokenId,
        CancellationToken cancellationToken = default)
    {
        await using var transaction = await transactions.BeginAsync(cancellationToken);
        var session = await sessions.GetByIdAsync(sessionId, cancellationToken)
            ?? throw new InvalidOperationException("Session was not found.");
        if (session.Status != SessionStatus.Running)
            throw new InvalidOperationException("Complete & call next is only available for a running session.");

        var current = await tokens.GetCurrentForUpdateAsync(sessionId, cancellationToken);
        if (current is null || current.Id != tokenId)
            throw new InvalidOperationException("The selected token is no longer the current patient.");

        var now = DateTime.UtcNow;
        current.Complete(now);
        var next = await tokens.GetNextWaitingForUpdateAsync(sessionId, cancellationToken);
        next?.Call(now);

        await tokens.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        if (events is not null)
            await events.PublishAsync(new QueueChangedEvent(
                QueueEventType.TokenCompletedAndNextCalled,
                sessionId,
                next?.Id ?? current.Id,
                next?.TokenNumber ?? current.TokenNumber,
                next?.Status ?? current.Status,
                session.Status,
                now), cancellationToken);

        return new CompleteAndCallNextResult(Map(current), next is null ? null : Map(next));
    }

    private static QueueTokenListItem Map(QueueToken token) => new(
        token.Id,
        token.TokenNumber,
        token.PatientProfileId,
        token.WalkInName,
        token.PhoneNumber,
        token.Status,
        token.CreatedAt,
        token.CalledAt,
        token.CompletedAt,
        token.PatientProfile?.Name ?? token.WalkInName,
        token.PatientProfile?.Age ?? token.WalkInAge,
        token.LateArrivedAt,
        token.QueueOrder);
}

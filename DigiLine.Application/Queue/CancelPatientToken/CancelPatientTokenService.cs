using DigiLine.Application.Patients;
using DigiLine.Application.Sessions;
using DigiLine.Application.Transactions;
using DigiLine.Domain.Common;

namespace DigiLine.Application.Queue;

public sealed class CancelPatientTokenService(
    IPatientAccessService access,
    IPublicPatientRepository patients,
    ISessionRepository sessions,
    IQueueTokenRepository tokens,
    ITransactionManager transactions,
    IQueueEventPublisher? events = null,
    TimeProvider? clock = null) : ICancelPatientTokenService
{
    private readonly TimeProvider _clock = clock ?? TimeProvider.System;

    public async Task<PublicTokenState?> CancelAsync(
        string accessToken,
        Guid sessionId,
        Guid tokenId,
        CancellationToken cancellationToken = default)
    {
        var phone = access.GetPhoneNumber(accessToken)
            ?? throw new UnauthorizedAccessException("Patient access has expired.");

        await using var transaction = await transactions.BeginAsync(cancellationToken);
        var session = await sessions.GetByIdAsync(sessionId, cancellationToken);
        if (session is null)
            return null;

        var token = await tokens.GetByIdForUpdateAsync(sessionId, tokenId, cancellationToken);
        if (token?.PatientProfileId is null
            || !await patients.IsOwnedByPhoneAsync(token.PatientProfileId.Value, phone, cancellationToken))
            return null;
        if (session.Status == SessionStatus.Closed)
            throw new InvalidOperationException("A token cannot be cancelled after the session is completed.");

        var now = _clock.GetUtcNow().UtcDateTime;
        token.CancelByPatient(now);
        await tokens.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        if (events is not null)
        {
            await events.PublishAsync(new QueueChangedEvent(
                QueueEventType.TokenCancelled,
                sessionId,
                token.Id,
                token.TokenNumber,
                token.Status,
                session.Status,
                now), cancellationToken);
        }

        var queue = await tokens.GetBySessionOrderedAsync(sessionId, cancellationToken);
        var current = queue
            .Where(item => item.Status is QueueTokenStatus.Called or QueueTokenStatus.InConsultation)
            .OrderByDescending(item => item.CalledAt ?? item.CreatedAt)
            .FirstOrDefault();

        return new PublicTokenState(
            sessionId,
            token.Id,
            token.TokenNumber,
            token.Status,
            current?.TokenNumber,
            null,
            null,
            null,
            session.Status == SessionStatus.Paused,
            session.Status,
            now);
    }
}

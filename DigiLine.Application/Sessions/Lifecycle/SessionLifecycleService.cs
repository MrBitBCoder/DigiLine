using DigiLine.Application.Queue;
using DigiLine.Application.Transactions;
using DigiLine.Domain.Common;

namespace DigiLine.Application.Sessions;

public interface ISessionLifecycleService
{
    Task<SessionLifecycleResult> StartAsync(Guid sessionId, CancellationToken cancellationToken = default);
    Task<SessionLifecycleResult> PauseAsync(Guid sessionId, CancellationToken cancellationToken = default);
    Task<SessionLifecycleResult> ResumeAsync(Guid sessionId, CancellationToken cancellationToken = default);
    Task<SessionLifecycleResult> CloseAsync(Guid sessionId, CancellationToken cancellationToken = default);
    Task<SessionLifecycleResult> StopBookingAsync(Guid sessionId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    Task<SessionLifecycleResult> CompleteAsync(Guid sessionId, bool confirmSkipped, CancellationToken cancellationToken = default) => throw new NotSupportedException();
}

public sealed class SessionCompletionException(string message, IReadOnlyDictionary<string, int> counts) : InvalidOperationException(message)
{
    public IReadOnlyDictionary<string, int> Counts { get; } = counts;
}

public sealed class SessionLifecycleService(ISessionRepository sessions, IQueueEventPublisher? events = null,
    ITransactionManager? transactions = null, TimeProvider? clock = null) : ISessionLifecycleService
{
    public Task<SessionLifecycleResult> StartAsync(Guid id, CancellationToken ct = default) => Change(id, s => s.Start(), QueueEventType.SessionStarted, ct);
    public Task<SessionLifecycleResult> PauseAsync(Guid id, CancellationToken ct = default) => Change(id, s => s.Pause(), QueueEventType.SessionPaused, ct);
    public Task<SessionLifecycleResult> ResumeAsync(Guid id, CancellationToken ct = default) => Change(id, s => s.Resume(), QueueEventType.SessionResumed, ct);
    public Task<SessionLifecycleResult> StopBookingAsync(Guid id, CancellationToken ct = default) => Change(id, s => s.StopBooking(Now), QueueEventType.BookingStopped, ct);
    public Task<SessionLifecycleResult> CloseAsync(Guid id, CancellationToken ct = default) => CompleteAsync(id, false, ct);
    private DateTime Now => (clock ?? TimeProvider.System).GetUtcNow().UtcDateTime;
    public async Task<SessionLifecycleResult> CompleteAsync(Guid id, bool confirmSkipped, CancellationToken ct = default)
    {
        await using var tx = transactions is null ? null : await transactions.BeginAsync(ct);
        var session = await sessions.GetByIdAsync(id, ct) ?? throw new InvalidOperationException("Session was not found.");
        if (session.Status == SessionStatus.Closed) return new(id, session.Status);
        var tokens = await sessions.GetUnfinishedTokensForUpdateAsync(id, ct);
        var counts = tokens.GroupBy(x => x.Status).ToDictionary(x => x.Key.ToString(), x => x.Count());
        if (tokens.Any(x => x.Status != QueueTokenStatus.Skipped))
            throw new SessionCompletionException("Resolve Waiting, Called, InConsultation and Late tokens before completion.", counts);
        if (tokens.Count > 0 && !confirmSkipped)
            throw new SessionCompletionException("Confirm that remaining skipped patients will be marked NoShow.", counts);
        var now = Now;
        session.CompleteSession(now);
        foreach (var token in tokens) token.FinalizeNoShow(now);
        await sessions.SaveChangesAsync(ct);
        if (tx is not null) await tx.CommitAsync(ct);
        await Publish(session, QueueEventType.SessionCompleted, ct);
        return new(id, session.Status);
    }
    private async Task<SessionLifecycleResult> Change(Guid id, Action<Domain.Entities.Session> change, QueueEventType type, CancellationToken ct)
    {
        await using var tx = transactions is null ? null : await transactions.BeginAsync(ct);
        var session = await sessions.GetByIdAsync(id, ct) ?? throw new InvalidOperationException("Session was not found.");
        change(session); await sessions.SaveChangesAsync(ct);
        if (tx is not null) await tx.CommitAsync(ct);
        await Publish(session, type, ct);
        return new(id, session.Status);
    }
    private Task Publish(Domain.Entities.Session session, QueueEventType type, CancellationToken ct) => events?.PublishAsync(
        new QueueChangedEvent(type, session.Id, null, null, null, session.Status, Now), ct) ?? Task.CompletedTask;
}

namespace DigiLine.Application.Sessions;

public sealed class StartSessionService : IStartSessionService
{
    private readonly ISessionRepository _sessions;

    public StartSessionService(ISessionRepository sessions) => _sessions = sessions;

    public async Task<SessionLifecycleResult> StartAsync(Guid sessionId, CancellationToken cancellationToken = default)
    {
        var session = await _sessions.GetByIdAsync(sessionId, cancellationToken)
            ?? throw new InvalidOperationException("Session was not found.");
        session.Start();
        await _sessions.SaveChangesAsync(cancellationToken);
        return new SessionLifecycleResult(session.Id, session.Status);
    }
}

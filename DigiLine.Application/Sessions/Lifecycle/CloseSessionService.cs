namespace DigiLine.Application.Sessions;

public sealed class CloseSessionService : ICloseSessionService
{
    private readonly ISessionRepository _sessions;

    public CloseSessionService(ISessionRepository sessions) => _sessions = sessions;

    public async Task<SessionLifecycleResult> CloseAsync(Guid sessionId, CancellationToken cancellationToken = default)
    {
        var session = await _sessions.GetByIdAsync(sessionId, cancellationToken)
            ?? throw new InvalidOperationException("Session was not found.");
        session.Close();
        await _sessions.SaveChangesAsync(cancellationToken);
        return new SessionLifecycleResult(session.Id, session.Status);
    }
}

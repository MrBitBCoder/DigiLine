namespace DigiLine.Application.Sessions;
public sealed class CloseSessionService(ISessionLifecycleService lifecycle) : ICloseSessionService
{
    public Task<SessionLifecycleResult> CloseAsync(Guid sessionId, CancellationToken cancellationToken = default) => lifecycle.CloseAsync(sessionId, cancellationToken);
}

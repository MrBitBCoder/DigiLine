namespace DigiLine.Application.Sessions;

public interface ICloseSessionService
{
    Task<SessionLifecycleResult> CloseAsync(Guid sessionId, CancellationToken cancellationToken = default);
}

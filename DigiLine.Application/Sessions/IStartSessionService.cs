namespace DigiLine.Application.Sessions;

public interface IStartSessionService
{
    Task<SessionLifecycleResult> StartAsync(Guid sessionId, CancellationToken cancellationToken = default);
}

namespace DigiLine.Application.Sessions;

public interface IPauseSessionService
{
    Task<PauseSessionResult> PauseAsync(
        Guid sessionId,
        CancellationToken cancellationToken = default);
}

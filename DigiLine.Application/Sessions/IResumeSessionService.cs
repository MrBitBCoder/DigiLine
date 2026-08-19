namespace DigiLine.Application.Sessions;

public interface IResumeSessionService
{
    Task<ResumeSessionResult> ResumeAsync(
        Guid sessionId,
        CancellationToken cancellationToken = default);
}

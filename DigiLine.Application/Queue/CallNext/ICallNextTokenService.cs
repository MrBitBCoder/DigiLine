namespace DigiLine.Application.Queue;

public interface ICallNextTokenService
{
    Task<QueueTokenListItem> CallAsync(
        Guid sessionId,
        CancellationToken cancellationToken = default);
}

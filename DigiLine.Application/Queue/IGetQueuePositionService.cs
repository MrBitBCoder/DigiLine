namespace DigiLine.Application.Queue;

public interface IGetQueuePositionService
{
    Task<QueuePositionResult> GetAsync(
        Guid sessionId,
        Guid tokenId,
        CancellationToken cancellationToken = default);
}

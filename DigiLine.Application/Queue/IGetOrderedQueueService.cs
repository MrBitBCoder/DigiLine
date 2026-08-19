namespace DigiLine.Application.Queue;

public interface IGetOrderedQueueService
{
    Task<IReadOnlyList<QueueTokenListItem>> GetAsync(
        Guid sessionId,
        CancellationToken cancellationToken = default);
}

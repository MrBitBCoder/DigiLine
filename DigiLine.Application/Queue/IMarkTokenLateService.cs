namespace DigiLine.Application.Queue;

public interface IMarkTokenLateService
{
    Task<QueueTokenListItem> MarkLateAsync(
        Guid sessionId,
        Guid tokenId,
        CancellationToken cancellationToken = default);
}

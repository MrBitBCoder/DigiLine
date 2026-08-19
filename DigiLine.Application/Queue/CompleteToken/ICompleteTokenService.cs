namespace DigiLine.Application.Queue;

public interface ICompleteTokenService
{
    Task<QueueTokenListItem> CompleteAsync(
        Guid sessionId,
        Guid tokenId,
        CancellationToken cancellationToken = default);
}

namespace DigiLine.Application.Queue;

public interface ICancelTokenService
{
    Task<QueueTokenListItem> CancelAsync(
        Guid sessionId,
        Guid tokenId,
        CancellationToken cancellationToken = default);
}

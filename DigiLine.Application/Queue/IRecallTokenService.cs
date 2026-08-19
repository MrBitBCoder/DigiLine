namespace DigiLine.Application.Queue;

public interface IRecallTokenService
{
    Task<QueueTokenListItem> RecallAsync(
        Guid sessionId,
        Guid tokenId,
        CancellationToken cancellationToken = default);
}

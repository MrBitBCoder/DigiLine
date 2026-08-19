namespace DigiLine.Application.Queue;

public interface ISkipTokenService
{
    Task<QueueTokenListItem> SkipAsync(
        Guid sessionId,
        Guid tokenId,
        CancellationToken cancellationToken = default);
}

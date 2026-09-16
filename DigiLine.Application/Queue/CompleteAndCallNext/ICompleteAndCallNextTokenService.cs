namespace DigiLine.Application.Queue;

public interface ICompleteAndCallNextTokenService
{
    Task<CompleteAndCallNextResult> CompleteAndCallNextAsync(
        Guid sessionId,
        Guid tokenId,
        CancellationToken cancellationToken = default);
}

public sealed record CompleteAndCallNextResult(
    QueueTokenListItem CompletedToken,
    QueueTokenListItem? CalledToken);

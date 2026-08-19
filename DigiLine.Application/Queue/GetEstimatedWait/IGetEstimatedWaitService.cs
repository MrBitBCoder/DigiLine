namespace DigiLine.Application.Queue;

public interface IGetEstimatedWaitService
{
    Task<EstimatedWaitResult> GetAsync(
        Guid sessionId,
        Guid tokenId,
        CancellationToken cancellationToken = default);
}

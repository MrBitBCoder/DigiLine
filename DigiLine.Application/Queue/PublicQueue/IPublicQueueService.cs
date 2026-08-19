namespace DigiLine.Application.Queue;

public interface IPublicQueueService
{
    Task<PublicBookingResult> BookAsync(string accessToken, Guid sessionId, Guid patientProfileId, CancellationToken cancellationToken = default);
    Task<PublicTokenState?> GetTokenAsync(string accessToken, Guid sessionId, Guid tokenId, CancellationToken cancellationToken = default);
}

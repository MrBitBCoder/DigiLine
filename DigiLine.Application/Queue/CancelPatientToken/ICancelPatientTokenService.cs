namespace DigiLine.Application.Queue;

public interface ICancelPatientTokenService
{
    Task<PublicTokenState?> CancelAsync(
        string accessToken,
        Guid sessionId,
        Guid tokenId,
        CancellationToken cancellationToken = default);
}

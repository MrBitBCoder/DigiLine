using DigiLine.Application.Sessions;

namespace DigiLine.Application.Queue;

public sealed class GetEstimatedWaitService : IGetEstimatedWaitService
{
    private readonly ISessionRepository _sessionRepository;
    private readonly IGetQueuePositionService _getQueuePositionService;

    public GetEstimatedWaitService(
        ISessionRepository sessionRepository,
        IGetQueuePositionService getQueuePositionService)
    {
        _sessionRepository = sessionRepository;
        _getQueuePositionService = getQueuePositionService;
    }

    public async Task<EstimatedWaitResult> GetAsync(
        Guid sessionId,
        Guid tokenId,
        CancellationToken cancellationToken = default)
    {
        var session = await _sessionRepository.GetByIdAsync(
            sessionId,
            cancellationToken);

        if (session is null)
        {
            throw new InvalidOperationException("Session was not found.");
        }

        var position = await _getQueuePositionService.GetAsync(
            sessionId,
            tokenId,
            cancellationToken);

        int? estimatedWaitMinutes = session.Status ==
            DigiLine.Domain.Common.SessionStatus.Paused
                ? null
                : checked(
                    position.PatientsAhead
                    * session.AverageConsultationMinutes);

        return new EstimatedWaitResult(
            position.TokenId,
            position.TokenNumber,
            position.Status,
            position.Position,
            position.PatientsAhead,
            session.AverageConsultationMinutes,
            session.Status == DigiLine.Domain.Common.SessionStatus.Paused,
            estimatedWaitMinutes);
    }
}

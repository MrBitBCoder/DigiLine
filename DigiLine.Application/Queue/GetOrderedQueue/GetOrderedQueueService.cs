using DigiLine.Application.Sessions;

namespace DigiLine.Application.Queue;

public sealed class GetOrderedQueueService : IGetOrderedQueueService
{
    private readonly ISessionRepository _sessionRepository;
    private readonly IQueueTokenRepository _queueTokenRepository;

    public GetOrderedQueueService(
        ISessionRepository sessionRepository,
        IQueueTokenRepository queueTokenRepository)
    {
        _sessionRepository = sessionRepository;
        _queueTokenRepository = queueTokenRepository;
    }

    public async Task<IReadOnlyList<QueueTokenListItem>> GetAsync(
        Guid sessionId,
        CancellationToken cancellationToken = default)
    {
        var session = await _sessionRepository.GetByIdAsync(
            sessionId,
            cancellationToken);

        if (session is null)
        {
            throw new InvalidOperationException("Session was not found.");
        }

        var tokens = await _queueTokenRepository.GetBySessionOrderedAsync(
            sessionId,
            cancellationToken);

        return tokens
            .OrderBy(token => token.QueueOrder)
            .Select(token => new QueueTokenListItem(
                token.Id,
                token.TokenNumber,
                token.PatientProfileId,
                token.WalkInName,
                token.PhoneNumber,
                token.Status,
                token.CreatedAt,
                token.CalledAt,
                token.CompletedAt,
                QueueOrder: token.QueueOrder))
            .ToList();
    }
}

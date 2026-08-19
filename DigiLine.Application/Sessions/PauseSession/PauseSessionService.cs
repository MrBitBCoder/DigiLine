using DigiLine.Application.Queue;

namespace DigiLine.Application.Sessions;

public sealed class PauseSessionService : IPauseSessionService
{
    private readonly ISessionRepository _sessionRepository;
    private readonly IQueueEventPublisher? _queueEventPublisher;

    public PauseSessionService(
        ISessionRepository sessionRepository,
        IQueueEventPublisher? queueEventPublisher = null)
    {
        _sessionRepository = sessionRepository;
        _queueEventPublisher = queueEventPublisher;
    }

    public async Task<PauseSessionResult> PauseAsync(
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

        session.Pause();
        await _sessionRepository.SaveChangesAsync(cancellationToken);

        if (_queueEventPublisher is not null)
        {
            await _queueEventPublisher.PublishAsync(
                new QueueChangedEvent(
                    QueueEventType.SessionPaused,
                    session.Id,
                    null,
                    null,
                    null,
                    session.Status,
                    DateTime.UtcNow),
                cancellationToken);
        }

        return new PauseSessionResult(session.Id, session.Status);
    }
}

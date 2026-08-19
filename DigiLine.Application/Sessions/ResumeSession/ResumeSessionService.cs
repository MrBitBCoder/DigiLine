using DigiLine.Application.Queue;

namespace DigiLine.Application.Sessions;

public sealed class ResumeSessionService : IResumeSessionService
{
    private readonly ISessionRepository _sessionRepository;
    private readonly IQueueEventPublisher? _queueEventPublisher;

    public ResumeSessionService(
        ISessionRepository sessionRepository,
        IQueueEventPublisher? queueEventPublisher = null)
    {
        _sessionRepository = sessionRepository;
        _queueEventPublisher = queueEventPublisher;
    }

    public async Task<ResumeSessionResult> ResumeAsync(
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

        session.Resume();
        await _sessionRepository.SaveChangesAsync(cancellationToken);

        if (_queueEventPublisher is not null)
        {
            await _queueEventPublisher.PublishAsync(
                new QueueChangedEvent(
                    QueueEventType.SessionResumed,
                    session.Id,
                    null,
                    null,
                    null,
                    session.Status,
                    DateTime.UtcNow),
                cancellationToken);
        }

        return new ResumeSessionResult(session.Id, session.Status);
    }
}

using DigiLine.Application.Queue;
using Microsoft.AspNetCore.SignalR;

namespace DigiLine.API.Realtime;

public sealed class SignalRQueueEventPublisher : IQueueEventPublisher
{
    private readonly IHubContext<QueueHub> _hubContext;
    private readonly ILogger<SignalRQueueEventPublisher> _logger;
    private readonly IHubContext<PublicQueueHub> _publicHubContext;

    public SignalRQueueEventPublisher(
        IHubContext<QueueHub> hubContext,
        IHubContext<PublicQueueHub> publicHubContext,
        ILogger<SignalRQueueEventPublisher> logger)
    {
        _hubContext = hubContext;
        _publicHubContext = publicHubContext;
        _logger = logger;
    }

    public async Task PublishAsync(
        QueueChangedEvent queueEvent,
        CancellationToken cancellationToken = default)
    {
        try
        {
            await _hubContext.Clients
                .Group(QueueHubContract.SessionGroup(queueEvent.SessionId))
                .SendAsync(
                    QueueHubContract.QueueChanged,
                    queueEvent,
                    cancellationToken);
            await _publicHubContext.Clients
                .Group(PublicQueueHub.PublicSessionGroup(queueEvent.SessionId))
                .SendAsync(
                    QueueHubContract.QueueChanged,
                    new { queueEvent.SessionId },
                    cancellationToken);
        }
        catch (Exception exception)
        {
            _logger.LogError(
                exception,
                "Failed to publish queue event {EventType} for session {SessionId}.",
                queueEvent.Type,
                queueEvent.SessionId);
        }
    }
}

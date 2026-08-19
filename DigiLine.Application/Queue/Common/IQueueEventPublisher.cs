namespace DigiLine.Application.Queue;

public interface IQueueEventPublisher
{
    Task PublishAsync(
        QueueChangedEvent queueEvent,
        CancellationToken cancellationToken = default);
}

using DigiLine.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using DigiLine.Application.Queue;
using Microsoft.AspNetCore.SignalR;

namespace DigiLine.API.Realtime;

public sealed class SignalRQueueEventPublisher : IQueueEventPublisher
{
    private readonly IServiceScopeFactory? _scopes;
    private readonly StaffSubscriptions? _subscriptions;
    private readonly IHubContext<QueueHub> _hubContext;
    private readonly ILogger<SignalRQueueEventPublisher> _logger;
    private readonly IHubContext<PublicQueueHub> _publicHubContext;

    public SignalRQueueEventPublisher(
        IHubContext<QueueHub> hubContext,
        IHubContext<PublicQueueHub> publicHubContext,
        ILogger<SignalRQueueEventPublisher> logger, IServiceScopeFactory? scopes = null, StaffSubscriptions? subscriptions = null)
    {
        _scopes = scopes; _subscriptions = subscriptions; _hubContext = hubContext;
        _publicHubContext = publicHubContext;
        _logger = logger;
    }

    public async Task PublishAsync(
        QueueChangedEvent queueEvent,
        CancellationToken cancellationToken = default)
    {
        try
        {
            if (_scopes is not null && _subscriptions is not null) {
                var subscribed = _subscriptions.ForSession(queueEvent.SessionId);
                if (subscribed.Length > 0) {
                    using var scope = _scopes.CreateScope();
                    var db = scope.ServiceProvider.GetRequiredService<DigiLineDbContext>();
                    var ids = subscribed.Select(x => x.StaffId).Distinct().ToArray();
                    var valid = await db.Staff.AsNoTracking().Where(x => ids.Contains(x.Id) && x.IsActive && !x.InvitationPending)
                        .Select(x => new { x.Id, x.PasswordVersion }).ToListAsync(cancellationToken);
                    var connections = subscribed.Where(x => valid.Any(s => s.Id == x.StaffId && s.PasswordVersion == x.Version)).Select(x => x.ConnectionId).ToArray();
                    if (connections.Length > 0) await _hubContext.Clients.Clients(connections).SendAsync(QueueHubContract.QueueChanged, queueEvent, cancellationToken);
                }
            }
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

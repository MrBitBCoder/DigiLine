using Microsoft.AspNetCore.SignalR;

namespace DigiLine.API.Realtime;

public sealed class QueueHub : Hub
{
    public Task JoinSession(Guid sessionId)
    {
        if (sessionId == Guid.Empty)
        {
            throw new HubException("A valid session ID is required.");
        }

        return Groups.AddToGroupAsync(
            Context.ConnectionId,
            QueueHubContract.SessionGroup(sessionId));
    }

    public Task LeaveSession(Guid sessionId)
    {
        if (sessionId == Guid.Empty)
        {
            throw new HubException("A valid session ID is required.");
        }

        return Groups.RemoveFromGroupAsync(
            Context.ConnectionId,
            QueueHubContract.SessionGroup(sessionId));
    }
}

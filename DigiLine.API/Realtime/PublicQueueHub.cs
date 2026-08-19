using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace DigiLine.API.Realtime;

[AllowAnonymous]
public sealed class PublicQueueHub : Hub
{
    public Task JoinSession(Guid sessionId)
    {
        if (sessionId == Guid.Empty) throw new HubException("A valid session ID is required.");
        return Groups.AddToGroupAsync(Context.ConnectionId, PublicSessionGroup(sessionId));
    }

    public Task LeaveSession(Guid sessionId) =>
        Groups.RemoveFromGroupAsync(Context.ConnectionId, PublicSessionGroup(sessionId));

    public static string PublicSessionGroup(Guid sessionId) => $"public-session:{sessionId:N}";
}

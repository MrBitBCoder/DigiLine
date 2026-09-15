using DigiLine.Application.Sessions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using System.Security.Claims;

namespace DigiLine.API.Realtime;

[Authorize(Roles = "Doctor,Receptionist")]
public sealed class QueueHub : Hub
{
    private readonly StaffSubscriptions? _subscriptions;
    private readonly ISessionRepository _sessions;

    public QueueHub(ISessionRepository sessions, StaffSubscriptions? subscriptions = null) { _sessions = sessions; _subscriptions = subscriptions; }
    public override Task OnDisconnectedAsync(Exception? exception) { _subscriptions?.RemoveConnection(Context.ConnectionId); return base.OnDisconnectedAsync(exception); }

    public async Task JoinSession(Guid sessionId)
    {
        if (sessionId == Guid.Empty)
        {
            throw new HubException("A valid session ID is required.");
        }

        if (!Guid.TryParse(Context.User?.FindFirstValue("clinic_id"), out var clinicId))
        {
            throw new HubException("The session is not available to this account.");
        }

        var session = await _sessions.GetByIdAsync(sessionId);

        var accountClaim = Context.User?.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? Context.User?.FindFirstValue("sub");
        var isDoctorOwner = Context.User!.IsInRole("Doctor") && Guid.TryParse(accountClaim, out var doctorId)
            && session?.DoctorId == doctorId;
        var isReceptionClinic = Context.User!.IsInRole("Receptionist");
        if (session?.ClinicId != clinicId || (!isDoctorOwner && !isReceptionClinic))
            throw new HubException("The session is not available to this account.");

        if (_subscriptions is not null && Guid.TryParse(accountClaim, out var staffId) &&
            Guid.TryParse(Context.User?.FindFirstValue("password_version"), out var version) &&
            long.TryParse(Context.User?.FindFirstValue("exp"), out var expires))
            _subscriptions.Add(new(Context.ConnectionId, sessionId, staffId, version, DateTimeOffset.FromUnixTimeSeconds(expires).UtcDateTime));
        await Groups.AddToGroupAsync(
            Context.ConnectionId,
            QueueHubContract.SessionGroup(sessionId));
    }

    public Task LeaveSession(Guid sessionId)
    {
        if (sessionId == Guid.Empty)
        {
            throw new HubException("A valid session ID is required.");
        }

        _subscriptions?.Remove(Context.ConnectionId, sessionId);
        return Groups.RemoveFromGroupAsync(
            Context.ConnectionId,
            QueueHubContract.SessionGroup(sessionId));
    }

}

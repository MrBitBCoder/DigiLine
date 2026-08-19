using DigiLine.Application.Sessions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using System.Security.Claims;

namespace DigiLine.API.Realtime;

[Authorize(Roles = "Doctor,Receptionist")]
public sealed class QueueHub : Hub
{
    private readonly ISessionRepository _sessions;

    public QueueHub(ISessionRepository sessions) => _sessions = sessions;

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
        var role = Context.User?.FindFirstValue(ClaimTypes.Role);
        var accountClaim = Context.User?.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? Context.User?.FindFirstValue("sub");
        var isDoctorOwner = role == "Doctor" && Guid.TryParse(accountClaim, out var doctorId)
            && session?.DoctorId == doctorId;
        var isReceptionClinic = role == "Receptionist";
        if (session?.ClinicId != clinicId || (!isDoctorOwner && !isReceptionClinic))
            throw new HubException("The session is not available to this account.");

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

        return Groups.RemoveFromGroupAsync(
            Context.ConnectionId,
            QueueHubContract.SessionGroup(sessionId));
    }

}

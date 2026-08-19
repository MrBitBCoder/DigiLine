using System.Security.Claims;
using DigiLine.API.Features.Queue;
using DigiLine.Application.Queue;
using DigiLine.Application.Sessions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DigiLine.API.Features.Reception;

[ApiController]
[Route("api/reception")]
[Authorize(Roles = "Receptionist")]
public sealed class ReceptionController : ControllerBase
{
    private readonly IGetReceptionDashboardService _dashboard;
    private readonly ICreateWalkInTokenService _walkIns;
    private readonly ICancelTokenService _cancel;
    private readonly IMarkTokenLateService _late;
    private readonly IRecallTokenService _recall;

    public ReceptionController(IGetReceptionDashboardService dashboard,
        ICreateWalkInTokenService walkIns, ICancelTokenService cancel,
        IMarkTokenLateService late, IRecallTokenService recall)
    {
        _dashboard = dashboard; _walkIns = walkIns; _cancel = cancel; _late = late; _recall = recall;
    }

    [HttpGet("sessions/today")]
    public async Task<IActionResult> Today(CancellationToken cancellationToken)
    {
        if (!TryGetClinic(out var clinicId)) return Unauthorized();
        return Ok(await _dashboard.GetTodayAsync(clinicId, cancellationToken));
    }

    [HttpPost("queue/walk-in")]
    public async Task<IActionResult> WalkIn([FromBody] CreateWalkInTokenRequest request, CancellationToken cancellationToken)
    {
        if (!TryGetClinic(out var clinicId)) return Unauthorized();
        if (!await HasSession(request.SessionId, clinicId, cancellationToken)) return NotFound();
        var tokenNumber = await _walkIns.CreateAsync(request.SessionId, request.WalkInName.Trim(),
            string.IsNullOrWhiteSpace(request.PhoneNumber) ? null : request.PhoneNumber.Trim(), cancellationToken);
        return Ok(new { tokenNumber });
    }

    [HttpPost("sessions/{sessionId:guid}/tokens/{tokenId:guid}/cancel")]
    public Task<IActionResult> Cancel(Guid sessionId, Guid tokenId, CancellationToken cancellationToken) =>
        Mutate(sessionId, tokenId, (s, t) => _cancel.CancelAsync(s, t, cancellationToken), cancellationToken);

    [HttpPost("sessions/{sessionId:guid}/tokens/{tokenId:guid}/late")]
    public Task<IActionResult> Late(Guid sessionId, Guid tokenId, CancellationToken cancellationToken) =>
        Mutate(sessionId, tokenId, (s, t) => _late.MarkLateAsync(s, t, cancellationToken), cancellationToken);

    [HttpPost("sessions/{sessionId:guid}/tokens/{tokenId:guid}/recall")]
    public Task<IActionResult> Recall(Guid sessionId, Guid tokenId, CancellationToken cancellationToken) =>
        Mutate(sessionId, tokenId, (s, t) => _recall.RecallAsync(s, t, cancellationToken), cancellationToken);

    private async Task<IActionResult> Mutate(Guid sessionId, Guid tokenId,
        Func<Guid, Guid, Task<QueueTokenListItem>> action, CancellationToken cancellationToken)
    {
        if (!TryGetClinic(out var clinicId)) return Unauthorized();
        if (!await HasSession(sessionId, clinicId, cancellationToken)) return NotFound();
        return Ok(await action(sessionId, tokenId));
    }

    private async Task<bool> HasSession(Guid sessionId, Guid clinicId, CancellationToken cancellationToken) =>
        await _dashboard.GetSessionAsync(sessionId, clinicId, cancellationToken) is not null;

    private bool TryGetClinic(out Guid clinicId) =>
        Guid.TryParse(User.FindFirstValue("clinic_id"), out clinicId);
}

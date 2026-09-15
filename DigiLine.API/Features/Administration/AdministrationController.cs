using System.Security.Claims;
using DigiLine.Application.Administration;
using DigiLine.Application.Common.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
namespace DigiLine.API.Features.Administration;

[ApiController, Route("api/auth")]
public sealed class RegistrationController(IAdministrationService administration, IAuthenticationService authentication) : ControllerBase
{
    [HttpPost("register"), EnableRateLimiting("password-reset")]
    public async Task<IActionResult> Register(RegisterClinicRequest request, CancellationToken ct)
    {
        await administration.RegisterAsync(request, ct);
        return Ok(new { token = await authentication.LoginAsync(request.Email, request.Password, ct) });
    }
    [HttpPost("accept-invitation"), EnableRateLimiting("password-reset")]
    public async Task<IActionResult> Accept(AcceptInvitationRequest request, CancellationToken ct)
    { await administration.AcceptAsync(request, ct); return NoContent(); }
}

[ApiController, Route("api/admin"), Authorize(Roles = "Admin")]
public sealed class AdministrationController(IAdministrationService administration) : ControllerBase
{
    private Guid ClinicId => Guid.Parse(User.FindFirstValue("clinic_id")!);
    private Guid ActorId => Guid.Parse((User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub"))!);
    [HttpGet("clinic")]
    public async Task<IActionResult> Clinic(CancellationToken ct) => Ok(await administration.GetClinicAsync(ClinicId, ct));
    [HttpPut("clinic")]
    public async Task<IActionResult> UpdateClinic(ClinicUpdateRequest request, CancellationToken ct)
    { await administration.UpdateClinicAsync(ClinicId, ActorId, request, ct); return NoContent(); }
    [HttpGet("staff")]
    public async Task<IActionResult> List(CancellationToken ct, int page = 1, int pageSize = 10) => Ok(await administration.ListAsync(ClinicId, page, pageSize, ct));
    [HttpPost("staff"), EnableRateLimiting("password-reset")]
    public async Task<IActionResult> Invite(InviteStaffRequest request, CancellationToken ct) => Ok(await administration.InviteAsync(ClinicId, ActorId, request, ct));
    [HttpPut("staff/{id:guid}")]
    public async Task<IActionResult> Update(Guid id, StaffUpdateRequest request, CancellationToken ct)
    { await administration.UpdateAsync(ClinicId, ActorId, id, request, ct); return NoContent(); }
    [HttpPost("staff/{id:guid}/resend"), EnableRateLimiting("password-reset")]
    public async Task<IActionResult> Resend(Guid id, CancellationToken ct) => Ok(new { delivered = await administration.ResendAsync(ClinicId, ActorId, id, ct) });
    [HttpPost("staff/{id:guid}/revoke")]
    public async Task<IActionResult> Revoke(Guid id, CancellationToken ct)
    { await administration.RevokeAsync(ClinicId, ActorId, id, ct); return NoContent(); }
}

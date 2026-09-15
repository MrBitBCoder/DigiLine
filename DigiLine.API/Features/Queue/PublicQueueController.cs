using DigiLine.Application.Queue;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace DigiLine.API.Features.Queue;

[ApiController]
[AllowAnonymous]
[Route("api/public/queue")]
public sealed class PublicQueueController : ControllerBase
{
    private readonly IPublicQueueService _queue;
    public PublicQueueController(IPublicQueueService queue) => _queue = queue;

    [HttpPost("book")]
    public async Task<IActionResult> Book(PublicBookingRequest request, [FromHeader(Name = "X-Patient-Access")] string accessToken, CancellationToken cancellationToken) =>
        Ok(await _queue.BookAsync(accessToken, request.SessionId, request.PatientProfileId, cancellationToken));

    [HttpPost("recover"), EnableRateLimiting("patient-recovery")]
    public async Task<IActionResult> Recover(PublicRecoveryRequest request, [FromHeader(Name = "X-Patient-Access")] string accessToken, CancellationToken cancellationToken)
    {
        var token = await _queue.RecoverAsync(accessToken, request.SessionId, request.PatientProfileId, cancellationToken);
        return token is null
            ? Problem(statusCode: StatusCodes.Status404NotFound, title: "Token not found.", detail: "We couldn't find an active token for this patient in this session.")
            : Ok(token);
    }

    [HttpGet("sessions/{sessionId:guid}/tokens/{tokenId:guid}")]
    public async Task<IActionResult> GetToken(Guid sessionId, Guid tokenId, [FromHeader(Name = "X-Patient-Access")] string accessToken, CancellationToken cancellationToken)
    {
        var token = await _queue.GetTokenAsync(accessToken, sessionId, tokenId, cancellationToken);
        return token is null ? NotFound() : Ok(token);
    }
}

public sealed record PublicBookingRequest(Guid SessionId, Guid PatientProfileId);
public sealed record PublicRecoveryRequest(Guid SessionId, Guid PatientProfileId);

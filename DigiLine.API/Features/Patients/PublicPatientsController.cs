using DigiLine.Application.Patients;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace DigiLine.API.Features.Patients;

[ApiController]
[AllowAnonymous]
[Route("api/public/patients")]
public sealed class PublicPatientsController : ControllerBase
{
    private readonly IPublicPatientService _patients;
    private readonly IWebHostEnvironment _environment;
    public PublicPatientsController(IPublicPatientService patients, IWebHostEnvironment environment)
    { _patients = patients; _environment = environment; }

    [HttpPost("verification"), EnableRateLimiting("patient-recovery")]
    public IActionResult RequestVerification(RequestVerificationRequest request)
    {
        var challenge = _patients.RequestVerification(request.PhoneNumber);
        return Accepted(new
        {
            challenge.ChallengeId,
            challenge.ExpiresAt,
            DevelopmentCode = _environment.IsDevelopment() ? challenge.VerificationCode : null
        });
    }

    [HttpPost("verification/confirm")]
    public async Task<IActionResult> ConfirmVerification(ConfirmVerificationRequest request, CancellationToken cancellationToken)
    {
        var access = await _patients.VerifyAsync(request.ChallengeId, request.Code, cancellationToken);
        return access is null ? Unauthorized(new ProblemDetails { Title = "The verification code is invalid or expired." }) : Ok(access);
    }

    [HttpPost("profiles")]
    public async Task<IActionResult> CreateProfile(CreatePublicProfileRequest request, [FromHeader(Name = "X-Patient-Access")] string accessToken, CancellationToken cancellationToken) =>
        Ok(await _patients.CreateProfileAsync(accessToken, request.Name, request.Age, cancellationToken));
}

public sealed record RequestVerificationRequest(string PhoneNumber);
public sealed record ConfirmVerificationRequest(Guid ChallengeId, string Code);
public sealed record CreatePublicProfileRequest(string Name, int Age);

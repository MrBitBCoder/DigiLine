using DigiLine.Application.Clinics;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DigiLine.API.Features.Clinics;

[ApiController]
[AllowAnonymous]
[Route("api/public/clinics")]
public sealed class PublicClinicsController : ControllerBase
{
    private readonly IGetPublicClinicService _clinics;

    public PublicClinicsController(IGetPublicClinicService clinics) =>
        _clinics = clinics;

    [HttpGet("{clinicSlug}")]
    public async Task<ActionResult<PublicClinicDetails>> Get(
        string clinicSlug,
        CancellationToken cancellationToken)
    {
        var clinic = await _clinics.GetAsync(clinicSlug, cancellationToken);
        return clinic is null ? NotFound() : Ok(clinic);
    }
}

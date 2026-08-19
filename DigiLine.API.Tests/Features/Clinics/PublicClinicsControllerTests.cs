using DigiLine.API.Features.Clinics;
using DigiLine.Application.Clinics;
using Microsoft.AspNetCore.Mvc;

namespace DigiLine.API.Tests;

public class PublicClinicsControllerTests
{
    [Fact]
    public async Task Get_WhenClinicExists_ReturnsPublicDetails()
    {
        var details = new PublicClinicDetails("Clinic", "clinic", []);
        var controller = new PublicClinicsController(new StubService(details));
        var result = await controller.Get("clinic", CancellationToken.None);
        Assert.Same(details, Assert.IsType<OkObjectResult>(result.Result).Value);
    }

    [Fact]
    public async Task Get_WhenClinicDoesNotExist_ReturnsNotFound()
    {
        var controller = new PublicClinicsController(new StubService(null));
        var result = await controller.Get("missing", CancellationToken.None);
        Assert.IsType<NotFoundResult>(result.Result);
    }

    private sealed class StubService(PublicClinicDetails? result)
        : IGetPublicClinicService
    {
        public Task<PublicClinicDetails?> GetAsync(string clinicSlug, CancellationToken cancellationToken = default) => Task.FromResult(result);
    }
}

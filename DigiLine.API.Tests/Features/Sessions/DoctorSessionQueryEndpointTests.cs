using System.Security.Claims;
using DigiLine.API.Features.Sessions;
using DigiLine.Application.Sessions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace DigiLine.API.Tests;

public class DoctorSessionQueryEndpointTests
{
    [Fact]
    public async Task List_UsesClinicAndDoctorClaimsAsQueryScope()
    {
        var clinicId = Guid.NewGuid(); var doctorId = Guid.NewGuid();
        var service = new StubQueryService();
        var controller = CreateController(service, clinicId, doctorId);

        var result = await controller.List(CancellationToken.None);

        Assert.IsType<OkObjectResult>(result);
        Assert.Equal(clinicId, service.ClinicId);
        Assert.Equal(doctorId, service.DoctorId);
    }

    [Fact]
    public async Task Get_WhenScopedSessionDoesNotExist_ReturnsNotFound()
    {
        var controller = CreateController(new StubQueryService(), Guid.NewGuid(), Guid.NewGuid());
        var result = await controller.Get(Guid.NewGuid(), CancellationToken.None);
        Assert.IsType<NotFoundResult>(result);
    }

    [Fact]
    public async Task List_WhenScopeClaimsAreMissing_ReturnsUnauthorized()
    {
        var controller = CreateController(new StubQueryService(), null, null);
        var result = await controller.List(CancellationToken.None);
        Assert.IsType<UnauthorizedResult>(result);
    }

    private static SessionsController CreateController(
        IGetDoctorSessionsService service, Guid? clinicId, Guid? doctorId)
    {
        var claims = new List<Claim>();
        if (clinicId.HasValue) claims.Add(new Claim("clinic_id", clinicId.Value.ToString()));
        if (doctorId.HasValue) claims.Add(new Claim(ClaimTypes.NameIdentifier, doctorId.Value.ToString()));
        return new SessionsController(
            new StubCreateSessionService(), new StubPauseSessionService(),
            new StubResumeSessionService(), service)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    User = new ClaimsPrincipal(new ClaimsIdentity(claims, "Test"))
                }
            }
        };
    }

    private sealed class StubQueryService : IGetDoctorSessionsService
    {
        public Guid ClinicId { get; private set; }
        public Guid DoctorId { get; private set; }
        public Task<IReadOnlyList<DoctorSessionSummary>> ListAsync(Guid clinicId, Guid doctorId, CancellationToken cancellationToken = default) { ClinicId = clinicId; DoctorId = doctorId; return Task.FromResult<IReadOnlyList<DoctorSessionSummary>>([]); }
        public Task<DoctorSessionDetails?> GetAsync(Guid sessionId, Guid clinicId, Guid doctorId, CancellationToken cancellationToken = default) { ClinicId = clinicId; DoctorId = doctorId; return Task.FromResult<DoctorSessionDetails?>(null); }
        public Task<DoctorSessionDetails?> GetActiveOrUpcomingAsync(Guid clinicId, Guid doctorId, CancellationToken cancellationToken = default) => Task.FromResult<DoctorSessionDetails?>(null);
    }

    private sealed class StubCreateSessionService : ICreateSessionService { public Task<Guid> CreateAsync(CreateSessionRequest request, CancellationToken cancellationToken = default) => throw new NotSupportedException(); }
    private sealed class StubPauseSessionService : IPauseSessionService { public Task<PauseSessionResult> PauseAsync(Guid sessionId, CancellationToken cancellationToken = default) => throw new NotSupportedException(); }
    private sealed class StubResumeSessionService : IResumeSessionService { public Task<ResumeSessionResult> ResumeAsync(Guid sessionId, CancellationToken cancellationToken = default) => throw new NotSupportedException(); }
}

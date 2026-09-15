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

    [Fact]
    public async Task Create_UsesAuthenticatedClinicAndDoctor()
    {
        var clinicId = Guid.NewGuid();
        var doctorId = Guid.NewGuid();
        var create = new StubCreateService();
        var controller = CreateController(new StubQueryService(), clinicId, doctorId, create);
        var start = DateTime.UtcNow.AddHours(2);

        var result = await controller.Create(
            new CreateDoctorSessionRequest("Morning", start, start.AddHours(2), 10), CancellationToken.None);

        Assert.IsType<OkObjectResult>(result);
        Assert.Equal(clinicId, create.ClinicId);
        Assert.Equal(doctorId, create.DoctorId);
    }

    private static SessionsController CreateController(
        IGetDoctorSessionsService service, Guid? clinicId, Guid? doctorId,
        StubCreateService? create = null)
    {
        var claims = new List<Claim>();
        if (clinicId.HasValue) claims.Add(new Claim("clinic_id", clinicId.Value.ToString()));
        if (doctorId.HasValue) claims.Add(new Claim(ClaimTypes.NameIdentifier, doctorId.Value.ToString()));
        return new SessionsController(new StubLifecycleService(), create ?? new StubCreateService(), service)
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

    private sealed class StubLifecycleService : ISessionLifecycleService
    {
        public Task<SessionLifecycleResult> StartAsync(Guid id, CancellationToken token = default) => throw new NotSupportedException();
        public Task<SessionLifecycleResult> PauseAsync(Guid id, CancellationToken token = default) => throw new NotSupportedException();
        public Task<SessionLifecycleResult> ResumeAsync(Guid id, CancellationToken token = default) => throw new NotSupportedException();
        public Task<SessionLifecycleResult> CloseAsync(Guid id, CancellationToken token = default) => throw new NotSupportedException();
    }

    private sealed class StubCreateService : ICreateDoctorSessionService
    {
        public Guid ClinicId { get; private set; }
        public Guid DoctorId { get; private set; }
        public Task<Guid> CreateAsync(Guid clinicId, Guid doctorId, CreateDoctorSessionRequest request,
            CancellationToken cancellationToken = default)
        {
            ClinicId = clinicId;
            DoctorId = doctorId;
            return Task.FromResult(Guid.NewGuid());
        }
    }
}

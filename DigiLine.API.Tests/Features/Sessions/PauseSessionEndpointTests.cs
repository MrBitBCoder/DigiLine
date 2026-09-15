using System.Security.Claims;
using DigiLine.API.Features.Sessions;
using DigiLine.Application.Sessions;
using DigiLine.Domain.Common;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace DigiLine.API.Tests;

public class PauseSessionEndpointTests
{
    [Theory]
    [InlineData("Start", SessionStatus.Running)]
    [InlineData("Pause", SessionStatus.Paused)]
    [InlineData("Resume", SessionStatus.Running)]
    [InlineData("Close", SessionStatus.Closed)]
    public async Task LifecycleAction_ReturnsResultAndPassesSessionId(string action, SessionStatus expectedStatus)
    {
        var clinicId = Guid.NewGuid();
        var doctorId = Guid.NewGuid();
        var sessionId = Guid.NewGuid();
        var lifecycle = new RecordingLifecycleService();
        var controller = CreateController(lifecycle, new OwnedSessionQuery(clinicId, doctorId, sessionId), clinicId, doctorId);

        var response = await Invoke(controller, action, sessionId);

        var result = Assert.IsType<SessionLifecycleResult>(Assert.IsType<OkObjectResult>(response).Value);
        Assert.Equal(sessionId, result.SessionId);
        Assert.Equal(expectedStatus, result.Status);
        Assert.Equal((action, sessionId), lifecycle.LastCall);
    }

    [Theory]
    [InlineData("Start")]
    [InlineData("Pause")]
    [InlineData("Resume")]
    [InlineData("Close")]
    public async Task LifecycleAction_RejectsForeignSessionBeforeMutation(string action)
    {
        var clinicId = Guid.NewGuid();
        var doctorId = Guid.NewGuid();
        var lifecycle = new RecordingLifecycleService();
        var controller = CreateController(lifecycle,
            new OwnedSessionQuery(Guid.NewGuid(), doctorId, Guid.NewGuid()), clinicId, doctorId);

        var response = await Invoke(controller, action, Guid.NewGuid());

        Assert.IsType<NotFoundResult>(response);
        Assert.Null(lifecycle.LastCall);
    }

    private static Task<IActionResult> Invoke(SessionsController controller, string action, Guid sessionId) => action switch
    {
        "Start" => controller.Start(sessionId, CancellationToken.None),
        "Pause" => controller.Pause(sessionId, CancellationToken.None),
        "Resume" => controller.Resume(sessionId, CancellationToken.None),
        "Close" => controller.Close(sessionId, CancellationToken.None),
        _ => throw new ArgumentOutOfRangeException(nameof(action))
    };

    private static SessionsController CreateController(
        ISessionLifecycleService lifecycle, IGetDoctorSessionsService query, Guid clinicId, Guid doctorId) =>
        new(lifecycle, new StubCreateService(), query)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    User = new ClaimsPrincipal(new ClaimsIdentity(
                    [
                        new Claim("clinic_id", clinicId.ToString()),
                        new Claim(ClaimTypes.NameIdentifier, doctorId.ToString())
                    ], "Test"))
                }
            }
        };

    private sealed class RecordingLifecycleService : ISessionLifecycleService
    {
        public (string Action, Guid SessionId)? LastCall { get; private set; }

        private Task<SessionLifecycleResult> Record(string action, Guid id, SessionStatus status)
        {
            LastCall = (action, id);
            return Task.FromResult(new SessionLifecycleResult(id, status));
        }

        public Task<SessionLifecycleResult> StartAsync(Guid id, CancellationToken cancellationToken = default) =>
            Record("Start", id, SessionStatus.Running);
        public Task<SessionLifecycleResult> PauseAsync(Guid id, CancellationToken cancellationToken = default) =>
            Record("Pause", id, SessionStatus.Paused);
        public Task<SessionLifecycleResult> ResumeAsync(Guid id, CancellationToken cancellationToken = default) =>
            Record("Resume", id, SessionStatus.Running);
        public Task<SessionLifecycleResult> CloseAsync(Guid id, CancellationToken cancellationToken = default) =>
            Record("Close", id, SessionStatus.Closed);
    }

    private sealed class StubCreateService : ICreateDoctorSessionService
    {
        public Task<Guid> CreateAsync(Guid clinicId, Guid doctorId, CreateDoctorSessionRequest request,
            CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }

    private sealed class OwnedSessionQuery(Guid clinicId, Guid doctorId, Guid sessionId) : IGetDoctorSessionsService
    {
        public Task<IReadOnlyList<DoctorSessionSummary>> ListAsync(Guid clinic, Guid doctor,
            CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public Task<DoctorSessionDetails?> GetAsync(Guid id, Guid clinic, Guid doctor,
            CancellationToken cancellationToken = default)
        {
            if (id != sessionId || clinic != clinicId || doctor != doctorId)
                return Task.FromResult<DoctorSessionDetails?>(null);
            var now = DateTime.UtcNow;
            var summary = new DoctorSessionSummary(id, clinic, "Clinic", doctor, "Doctor", "Morning",
                now.AddHours(-1), now, now.AddHours(2), now.AddHours(2), 10,
                SessionStatus.Running, true, new DoctorQueueSummary(null, 0, 0, 0, 0, 0));
            return Task.FromResult<DoctorSessionDetails?>(new DoctorSessionDetails(summary, []));
        }

        public Task<DoctorSessionDetails?> GetActiveOrUpcomingAsync(Guid clinic, Guid doctor,
            CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }
}

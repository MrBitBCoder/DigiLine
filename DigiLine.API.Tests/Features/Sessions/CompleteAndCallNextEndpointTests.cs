using System.Security.Claims;
using DigiLine.API.Features.Sessions;
using DigiLine.Application.Queue;
using DigiLine.Application.Sessions;
using DigiLine.Domain.Common;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace DigiLine.API.Tests;

public class CompleteAndCallNextEndpointTests
{
    [Fact]
    public async Task CompleteAndCallNext_UsesOwnedSessionAndReturnsCombinedResult()
    {
        var clinicId = Guid.NewGuid();
        var doctorId = Guid.NewGuid();
        var sessionId = Guid.NewGuid();
        var tokenId = Guid.NewGuid();
        var service = new RecordingService();
        var controller = CreateController(clinicId, doctorId,
            new OwnedSessionQuery(clinicId, doctorId, sessionId), service);

        var response = await controller.CompleteAndCallNext(sessionId, tokenId, CancellationToken.None);

        var result = Assert.IsType<CompleteAndCallNextResult>(Assert.IsType<OkObjectResult>(response).Value);
        Assert.Equal(tokenId, result.CompletedToken.Id);
        Assert.Equal((sessionId, tokenId), service.LastCall);
    }

    [Fact]
    public async Task CompleteAndCallNext_RejectsForeignSessionBeforeMutation()
    {
        var clinicId = Guid.NewGuid();
        var doctorId = Guid.NewGuid();
        var service = new RecordingService();
        var controller = CreateController(clinicId, doctorId,
            new OwnedSessionQuery(Guid.NewGuid(), doctorId, Guid.NewGuid()), service);

        var response = await controller.CompleteAndCallNext(Guid.NewGuid(), Guid.NewGuid(), CancellationToken.None);

        Assert.IsType<NotFoundResult>(response);
        Assert.Null(service.LastCall);
    }

    private static SessionsController CreateController(Guid clinicId, Guid doctorId,
        IGetDoctorSessionsService query, ICompleteAndCallNextTokenService service) =>
        new(new StubLifecycle(), new StubCreate(), query, completeAndCallNextService: service)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    User = new ClaimsPrincipal(new ClaimsIdentity([
                        new Claim("clinic_id", clinicId.ToString()),
                        new Claim(ClaimTypes.NameIdentifier, doctorId.ToString())
                    ], "Test"))
                }
            }
        };

    private sealed class RecordingService : ICompleteAndCallNextTokenService
    {
        public (Guid SessionId, Guid TokenId)? LastCall { get; private set; }
        public Task<CompleteAndCallNextResult> CompleteAndCallNextAsync(Guid sessionId, Guid tokenId,
            CancellationToken cancellationToken = default)
        {
            LastCall = (sessionId, tokenId);
            var now = DateTime.UtcNow;
            return Task.FromResult(new CompleteAndCallNextResult(
                new QueueTokenListItem(tokenId, 1, null, "Patient", null, QueueTokenStatus.Completed, now, now, now),
                null));
        }
    }

    private sealed class OwnedSessionQuery(Guid clinicId, Guid doctorId, Guid sessionId) : IGetDoctorSessionsService
    {
        public Task<DoctorSessionDetails?> GetAsync(Guid id, Guid clinic, Guid doctor, CancellationToken ct = default) =>
            Task.FromResult<DoctorSessionDetails?>(id == sessionId && clinic == clinicId && doctor == doctorId
                ? new DoctorSessionDetails(new DoctorSessionSummary(id, clinic, "Clinic", doctor, "Doctor", "Session",
                    DateTime.UtcNow, DateTime.UtcNow, DateTime.UtcNow.AddHours(1), DateTime.UtcNow.AddHours(1), 10,
                    SessionStatus.Running, true, new DoctorQueueSummary(null, 0, 0, 0, 0, 0)), [])
                : null);
        public Task<IReadOnlyList<DoctorSessionSummary>> ListAsync(Guid clinic, Guid doctor, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<DoctorSessionDetails?> GetActiveOrUpcomingAsync(Guid clinic, Guid doctor, CancellationToken ct = default) => throw new NotSupportedException();
    }

    private sealed class StubLifecycle : ISessionLifecycleService
    {
        public Task<SessionLifecycleResult> StartAsync(Guid id, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<SessionLifecycleResult> PauseAsync(Guid id, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<SessionLifecycleResult> ResumeAsync(Guid id, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<SessionLifecycleResult> CloseAsync(Guid id, CancellationToken ct = default) => throw new NotSupportedException();
    }
    private sealed class StubCreate : ICreateDoctorSessionService
    {
        public Task<Guid> CreateAsync(Guid clinicId, Guid doctorId, CreateDoctorSessionRequest request,
            CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }
}

using DigiLine.API.Features.Sessions;
using DigiLine.Application.Sessions;
using DigiLine.Domain.Common;
using Microsoft.AspNetCore.Mvc;

namespace DigiLine.API.Tests;

public class PauseSessionEndpointTests
{
    [Fact]
    public async Task Pause_ReturnsPausedSessionResult()
    {
        var sessionId = Guid.NewGuid();
        var pauseService = new StubPauseSessionService(
            new PauseSessionResult(sessionId, SessionStatus.Paused));
        var controller = new SessionsController(
            new StubCreateSessionService(),
            pauseService,
            new StubResumeSessionService(null));

        var action = await controller.Pause(sessionId, CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(action);
        var result = Assert.IsType<PauseSessionResult>(ok.Value);
        Assert.Equal(sessionId, result.SessionId);
        Assert.Equal(SessionStatus.Paused, result.Status);
        Assert.Equal(sessionId, pauseService.SessionId);
    }

    [Fact]
    public async Task Resume_ReturnsRunningSessionResult()
    {
        var sessionId = Guid.NewGuid();
        var resumeService = new StubResumeSessionService(
            new ResumeSessionResult(sessionId, SessionStatus.Running));
        var controller = new SessionsController(
            new StubCreateSessionService(),
            new StubPauseSessionService(null),
            resumeService);

        var action = await controller.Resume(sessionId, CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(action);
        var result = Assert.IsType<ResumeSessionResult>(ok.Value);
        Assert.Equal(sessionId, result.SessionId);
        Assert.Equal(SessionStatus.Running, result.Status);
        Assert.Equal(sessionId, resumeService.SessionId);
    }

    private sealed class StubPauseSessionService : IPauseSessionService
    {
        private readonly PauseSessionResult? _result;
        public StubPauseSessionService(PauseSessionResult? result) => _result = result;
        public Guid SessionId { get; private set; }

        public Task<PauseSessionResult> PauseAsync(
            Guid sessionId,
            CancellationToken cancellationToken = default)
        {
            SessionId = sessionId;
            return Task.FromResult(_result!);
        }
    }

    private sealed class StubResumeSessionService : IResumeSessionService
    {
        private readonly ResumeSessionResult? _result;
        public StubResumeSessionService(ResumeSessionResult? result) => _result = result;
        public Guid SessionId { get; private set; }

        public Task<ResumeSessionResult> ResumeAsync(
            Guid sessionId,
            CancellationToken cancellationToken = default)
        {
            SessionId = sessionId;
            return Task.FromResult(_result!);
        }
    }

    private sealed class StubCreateSessionService : ICreateSessionService
    {
        public Task<Guid> CreateAsync(
            CreateSessionRequest request,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
    }
}

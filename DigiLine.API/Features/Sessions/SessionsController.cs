using DigiLine.Application.Sessions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DigiLine.API.Features.Sessions
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize(Roles = "Doctor")]
    public class SessionsController : ControllerBase
    {
        private readonly ICreateSessionService _createSessionService;
        private readonly IPauseSessionService _pauseSessionService;
        private readonly IResumeSessionService _resumeSessionService;

        public SessionsController(
            ICreateSessionService createSessionService,
            IPauseSessionService pauseSessionService,
            IResumeSessionService resumeSessionService)
        {
            _createSessionService = createSessionService;
            _pauseSessionService = pauseSessionService;
            _resumeSessionService = resumeSessionService;
        }

        [HttpPost]
        public async Task<IActionResult> Create(
            [FromBody] CreateSessionRequest request,
            CancellationToken cancellationToken)
        {
            var sessionId = await _createSessionService.CreateAsync(
                request,
                cancellationToken);

            return Ok(new
            {
                id = sessionId
            });
        }

        [HttpPost("{sessionId:guid}/pause")]
        public async Task<IActionResult> Pause(
            Guid sessionId,
            CancellationToken cancellationToken)
        {
            var result = await _pauseSessionService.PauseAsync(
                sessionId,
                cancellationToken);

            return Ok(result);
        }

        [HttpPost("{sessionId:guid}/resume")]
        public async Task<IActionResult> Resume(
            Guid sessionId,
            CancellationToken cancellationToken)
        {
            var result = await _resumeSessionService.ResumeAsync(
                sessionId,
                cancellationToken);

            return Ok(result);
        }
    }
}

using DigiLine.Application.Sessions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

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
        private readonly IGetDoctorSessionsService? _getDoctorSessionsService;
        private readonly IStartSessionService? _startSessionService;
        private readonly ICloseSessionService? _closeSessionService;

        public SessionsController(
            ICreateSessionService createSessionService,
            IPauseSessionService pauseSessionService,
            IResumeSessionService resumeSessionService,
            IGetDoctorSessionsService? getDoctorSessionsService = null,
            IStartSessionService? startSessionService = null,
            ICloseSessionService? closeSessionService = null)
        {
            _createSessionService = createSessionService;
            _pauseSessionService = pauseSessionService;
            _resumeSessionService = resumeSessionService;
            _getDoctorSessionsService = getDoctorSessionsService;
            _startSessionService = startSessionService;
            _closeSessionService = closeSessionService;
        }

        [HttpGet]
        public async Task<IActionResult> List(CancellationToken cancellationToken)
        {
            if (!TryGetScope(out var clinicId, out var doctorId)) return Unauthorized();
            var sessions = await QueryService.ListAsync(clinicId, doctorId, cancellationToken);
            return Ok(sessions);
        }

        [HttpGet("active-upcoming")]
        public async Task<IActionResult> GetActiveOrUpcoming(CancellationToken cancellationToken)
        {
            if (!TryGetScope(out var clinicId, out var doctorId)) return Unauthorized();
            var session = await QueryService.GetActiveOrUpcomingAsync(clinicId, doctorId, cancellationToken);
            return session is null ? NoContent() : Ok(session);
        }

        [HttpGet("{sessionId:guid}")]
        public async Task<IActionResult> Get(Guid sessionId, CancellationToken cancellationToken)
        {
            if (!TryGetScope(out var clinicId, out var doctorId)) return Unauthorized();
            var session = await QueryService.GetAsync(sessionId, clinicId, doctorId, cancellationToken);
            return session is null ? NotFound() : Ok(session);
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

        [HttpPost("{sessionId:guid}/start")]
        public async Task<IActionResult> Start(Guid sessionId, CancellationToken cancellationToken)
        {
            var service = _startSessionService ?? throw new InvalidOperationException(
                "Start session is not configured.");
            return Ok(await service.StartAsync(sessionId, cancellationToken));
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

        [HttpPost("{sessionId:guid}/close")]
        public async Task<IActionResult> Close(Guid sessionId, CancellationToken cancellationToken)
        {
            var service = _closeSessionService ?? throw new InvalidOperationException(
                "Close session is not configured.");
            return Ok(await service.CloseAsync(sessionId, cancellationToken));
        }

        private IGetDoctorSessionsService QueryService =>
            _getDoctorSessionsService ?? throw new InvalidOperationException(
                "Doctor session queries are not configured.");

        private bool TryGetScope(out Guid clinicId, out Guid doctorId)
        {
            var doctorClaim = User.FindFirstValue(ClaimTypes.NameIdentifier)
                ?? User.FindFirstValue("sub");
            var hasClinic = Guid.TryParse(
                User.FindFirstValue("clinic_id"), out clinicId);
            var hasDoctor = Guid.TryParse(doctorClaim, out doctorId);
            return hasClinic && hasDoctor;
        }
    }
}

using DigiLine.Application.Sessions;
using DigiLine.Application.Queue;
using DigiLine.Application.Clinics;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace DigiLine.API.Features.Sessions
{
    [ApiController]
    [Route("api/doctor")]
    [Authorize(Roles = "Doctor")]
    public class SessionsController : ControllerBase
    {
        private readonly IStaffSessionQueries? _paged;
        private readonly ISessionLifecycleService _lifecycleService;
        private readonly IClinicTimeZoneResolver? _timeZones;
        private readonly ICreateDoctorSessionService _createSessionService;
        private readonly IManageDoctorSessionService? _manageSessionService;
        private readonly IGetDoctorSessionsService? _getDoctorSessionsService;
        private readonly ICallNextTokenService? _callNextService;
        private readonly IRecallTokenService? _recallService;
        private readonly ISkipTokenService? _skipService;
        private readonly ICompleteTokenService? _completeService;
        private readonly IMarkTokenLateService? _lateService;

        public SessionsController(
            ISessionLifecycleService lifecycleService,
            ICreateDoctorSessionService createSessionService,
            IGetDoctorSessionsService? getDoctorSessionsService = null,
            ICallNextTokenService? callNextService = null,
            IRecallTokenService? recallService = null,
            ISkipTokenService? skipService = null,
            ICompleteTokenService? completeService = null,
            IMarkTokenLateService? lateService = null,
            IManageDoctorSessionService? manageSessionService = null,
            IClinicTimeZoneResolver? timeZones = null, IStaffSessionQueries? paged = null)
        {
            _paged = paged;
            _lifecycleService = lifecycleService;
            _createSessionService = createSessionService;
            _getDoctorSessionsService = getDoctorSessionsService;
            _callNextService = callNextService;
            _recallService = recallService;
            _skipService = skipService;
            _completeService = completeService;
            _lateService = lateService;
            _manageSessionService = manageSessionService;
            _timeZones = timeZones;
        }

        [HttpGet("clinic-time-zone")]
        public async Task<IActionResult> ClinicTimeZone(CancellationToken cancellationToken)
        {
            if (!TryGetScope(out var clinicId, out _)) return Unauthorized();
            return Ok(new { timeZoneId = await Required(_timeZones).GetTimeZoneIdAsync(clinicId, cancellationToken) });
        }

        [HttpGet("sessions")]
        public async Task<IActionResult> List(CancellationToken cancellationToken, [FromQuery] SessionPageQuery? query = null)
        {
            if (!TryGetScope(out var clinicId, out var doctorId)) return Unauthorized();
            if (_paged is not null) return Ok(await _paged.ListAsync(clinicId, doctorId, query ?? new(), cancellationToken));
            var sessions = await QueryService.ListAsync(clinicId, doctorId, cancellationToken);
            return Ok(sessions);
        }

        [HttpPost("sessions")]
        public async Task<IActionResult> Create(
            [FromBody] CreateDoctorSessionRequest request,
            CancellationToken cancellationToken)
        {
            if (!TryGetScope(out var clinicId, out var doctorId)) return Unauthorized();
            var id = await _createSessionService.CreateAsync(
                clinicId, doctorId, request, cancellationToken);
            return Ok(new { id });
        }

        [HttpPut("sessions/{sessionId:guid}")]
        public async Task<IActionResult> Update(Guid sessionId, [FromBody] CreateDoctorSessionRequest request,
            CancellationToken cancellationToken)
        {
            if (!TryGetScope(out var clinicId, out var doctorId)) return Unauthorized();
            try
            {
                await Required(_manageSessionService).UpdateAsync(clinicId, doctorId, sessionId, request, cancellationToken);
                return NoContent();
            }
            catch (KeyNotFoundException) { return NotFound(); }
        }

        [HttpDelete("sessions/{sessionId:guid}")]
        public async Task<IActionResult> Delete(Guid sessionId, CancellationToken cancellationToken)
        {
            if (!TryGetScope(out var clinicId, out var doctorId)) return Unauthorized();
            try
            {
                await Required(_manageSessionService).DeleteAsync(clinicId, doctorId, sessionId, cancellationToken);
                return NoContent();
            }
            catch (KeyNotFoundException) { return NotFound(); }
        }

        [HttpGet("sessions/active-upcoming")]
        public async Task<IActionResult> GetActiveOrUpcoming(CancellationToken cancellationToken)
        {
            if (!TryGetScope(out var clinicId, out var doctorId)) return Unauthorized();
            var session = _paged is not null ? await _paged.ActiveAsync(clinicId, doctorId, cancellationToken) : await QueryService.GetActiveOrUpcomingAsync(clinicId, doctorId, cancellationToken);
            return session is null ? NoContent() : Ok(session);
        }

        [HttpGet("sessions/{sessionId:guid}")]
        public async Task<IActionResult> Get(Guid sessionId, CancellationToken cancellationToken, int page = 1, int pageSize = 10, string filter = "Active")
        {
            if (!TryGetScope(out var clinicId, out var doctorId)) return Unauthorized();
            var session = _paged is not null ? await _paged.DetailsAsync(clinicId, doctorId, sessionId, page, pageSize, filter, cancellationToken) : await QueryService.GetAsync(sessionId, clinicId, doctorId, cancellationToken);
            return session is null ? NotFound() : Ok(session);
        }

        [HttpPost("sessions/{sessionId:guid}/pause")]
        public async Task<IActionResult> Pause(
            Guid sessionId,
            CancellationToken cancellationToken)
        {
            if (!await OwnsSession(sessionId, cancellationToken)) return NotFound();
            var result = await _lifecycleService.PauseAsync(
                sessionId,
                cancellationToken);

            return Ok(result);
        }

        [HttpPost("sessions/{sessionId:guid}/start")]
        public async Task<IActionResult> Start(Guid sessionId, CancellationToken cancellationToken)
        {
            if (!await OwnsSession(sessionId, cancellationToken)) return NotFound();
            return Ok(await _lifecycleService.StartAsync(sessionId, cancellationToken));
        }

        [HttpPost("sessions/{sessionId:guid}/resume")]
        public async Task<IActionResult> Resume(
            Guid sessionId,
            CancellationToken cancellationToken)
        {
            if (!await OwnsSession(sessionId, cancellationToken)) return NotFound();
            var result = await _lifecycleService.ResumeAsync(
                sessionId,
                cancellationToken);

            return Ok(result);
        }

        public sealed record CompleteSessionRequest(bool ConfirmSkipped);
        [HttpPost("sessions/{sessionId:guid}/stop-booking")]
        public async Task<IActionResult> StopBooking(Guid sessionId, CancellationToken ct)
        {
            if (!await OwnsSession(sessionId, ct)) return NotFound();
            return Ok(await _lifecycleService.StopBookingAsync(sessionId, ct));
        }
        [HttpPost("sessions/{sessionId:guid}/complete")]
        public async Task<IActionResult> CompleteSession(Guid sessionId, CompleteSessionRequest request, CancellationToken ct)
        {
            if (!await OwnsSession(sessionId, ct)) return NotFound();
            return Ok(await _lifecycleService.CompleteAsync(sessionId, request.ConfirmSkipped, ct));
        }

        [HttpPost("sessions/{sessionId:guid}/close")]
        public async Task<IActionResult> Close(Guid sessionId, CancellationToken cancellationToken)
        {
            if (!await OwnsSession(sessionId, cancellationToken)) return NotFound();
            return Ok(await _lifecycleService.CloseAsync(sessionId, cancellationToken));
        }

        [HttpPost("sessions/{sessionId:guid}/queue/call-next")]
        public Task<IActionResult> CallNext(Guid sessionId, CancellationToken cancellationToken) =>
            RunQueueAction(sessionId, () => Required(_callNextService).CallAsync(sessionId, cancellationToken), cancellationToken);

        [HttpPost("sessions/{sessionId:guid}/tokens/{tokenId:guid}/recall")]
        public Task<IActionResult> Recall(Guid sessionId, Guid tokenId, CancellationToken cancellationToken) =>
            RunQueueAction(sessionId, () => Required(_recallService).RecallAsync(sessionId, tokenId, cancellationToken), cancellationToken);

        [HttpPost("sessions/{sessionId:guid}/tokens/{tokenId:guid}/skip")]
        public Task<IActionResult> Skip(Guid sessionId, Guid tokenId, CancellationToken cancellationToken) =>
            RunQueueAction(sessionId, () => Required(_skipService).SkipAsync(sessionId, tokenId, cancellationToken), cancellationToken);

        [HttpPost("sessions/{sessionId:guid}/tokens/{tokenId:guid}/complete")]
        public Task<IActionResult> Complete(Guid sessionId, Guid tokenId, CancellationToken cancellationToken) =>
            RunQueueAction(sessionId, () => Required(_completeService).CompleteAsync(sessionId, tokenId, cancellationToken), cancellationToken);

        [HttpPost("sessions/{sessionId:guid}/tokens/{tokenId:guid}/late")]
        public Task<IActionResult> Late(Guid sessionId, Guid tokenId, CancellationToken cancellationToken) =>
            RunQueueAction(sessionId, () => Required(_lateService).MarkLateAsync(sessionId, tokenId, cancellationToken), cancellationToken);

        private async Task<IActionResult> RunQueueAction(Guid sessionId, Func<Task<QueueTokenListItem>> action, CancellationToken cancellationToken)
        {
            if (!await OwnsSession(sessionId, cancellationToken)) return NotFound();
            return Ok(await action());
        }

        private async Task<bool> OwnsSession(Guid sessionId, CancellationToken cancellationToken)
        {
            if (!TryGetScope(out var clinicId, out var doctorId)) return false;
            return _paged is not null ? await _paged.OwnsAsync(clinicId, doctorId, sessionId, cancellationToken) : await QueryService.GetAsync(sessionId, clinicId, doctorId, cancellationToken) is not null;
        }

        private static T Required<T>(T? service) where T : class => service
            ?? throw new InvalidOperationException($"{typeof(T).Name} is not configured.");

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

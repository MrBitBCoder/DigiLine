using DigiLine.Application.Sessions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DigiLine.API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize(Roles = "Doctor")]
    public class SessionsController : ControllerBase
    {
        private readonly ICreateSessionService _createSessionService;

        public SessionsController(
            ICreateSessionService createSessionService)
        {
            _createSessionService = createSessionService;
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
    }
}

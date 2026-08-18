using DigiLine.Application.Queue;
using Microsoft.AspNetCore.Mvc;

namespace DigiLine.API.Queue
{
    [ApiController]
    [Route("api/[controller]")]
    public class QueueController : ControllerBase
    {
        private readonly IBookTokenService _bookTokenService;

        public QueueController(
            IBookTokenService bookTokenService)
        {
            _bookTokenService = bookTokenService;
        }

        [HttpPost("book")]
        public async Task<IActionResult> Book(
            [FromBody] BookTokenRequest request,
            CancellationToken cancellationToken)
        {
            var tokenNumber = await _bookTokenService.BookAsync(
                request.SessionId,
                request.PatientProfileId,
                cancellationToken);

            return Ok(new
            {
                tokenNumber
            });
        }
    }
}

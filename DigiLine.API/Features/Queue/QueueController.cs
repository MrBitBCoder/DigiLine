using DigiLine.Application.Queue;
using Microsoft.AspNetCore.Mvc;

namespace DigiLine.API.Features.Queue
{
    [NonController] // Legacy unscoped endpoints are retired; use public/doctor/reception routes.
    [Route("api/[controller]")]
    public class QueueController : ControllerBase
    {
        private readonly IBookTokenService _bookTokenService;
        private readonly ICreateWalkInTokenService _createWalkInTokenService;
        private readonly IGetOrderedQueueService _getOrderedQueueService;
        private readonly ICallNextTokenService _callNextTokenService;
        private readonly ISkipTokenService _skipTokenService;
        private readonly ICompleteTokenService _completeTokenService;
        private readonly ICancelTokenService _cancelTokenService;
        private readonly IMarkTokenLateService _markTokenLateService;
        private readonly IGetQueuePositionService _getQueuePositionService;
        private readonly IGetEstimatedWaitService _getEstimatedWaitService;

        public QueueController(
            IBookTokenService bookTokenService,
            ICreateWalkInTokenService createWalkInTokenService,
            IGetOrderedQueueService getOrderedQueueService,
            ICallNextTokenService callNextTokenService,
            ISkipTokenService skipTokenService,
            ICompleteTokenService completeTokenService,
            ICancelTokenService cancelTokenService,
            IMarkTokenLateService markTokenLateService,
            IGetQueuePositionService getQueuePositionService,
            IGetEstimatedWaitService getEstimatedWaitService)
        {
            _bookTokenService = bookTokenService;
            _createWalkInTokenService = createWalkInTokenService;
            _getOrderedQueueService = getOrderedQueueService;
            _callNextTokenService = callNextTokenService;
            _skipTokenService = skipTokenService;
            _completeTokenService = completeTokenService;
            _cancelTokenService = cancelTokenService;
            _markTokenLateService = markTokenLateService;
            _getQueuePositionService = getQueuePositionService;
            _getEstimatedWaitService = getEstimatedWaitService;
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

        [HttpPost("walk-in")]
        public async Task<IActionResult> CreateWalkIn(
            [FromBody] CreateWalkInTokenRequest request,
            CancellationToken cancellationToken)
        {
            var tokenNumber = await _createWalkInTokenService.CreateAsync(
                request.SessionId,
                request.WalkInName,
                request.PhoneNumber,
                cancellationToken,
                request.Age);

            return Ok(new
            {
                tokenNumber
            });
        }

        [HttpGet("session/{sessionId:guid}")]
        public async Task<IActionResult> GetOrderedQueue(
            Guid sessionId,
            CancellationToken cancellationToken)
        {
            var tokens = await _getOrderedQueueService.GetAsync(
                sessionId,
                cancellationToken);

            return Ok(tokens);
        }

        [HttpPost("session/{sessionId:guid}/call-next")]
        public async Task<IActionResult> CallNext(
            Guid sessionId,
            CancellationToken cancellationToken)
        {
            var token = await _callNextTokenService.CallAsync(
                sessionId,
                cancellationToken);

            return Ok(token);
        }

        [HttpPost("session/{sessionId:guid}/token/{tokenId:guid}/skip")]
        public async Task<IActionResult> Skip(
            Guid sessionId,
            Guid tokenId,
            CancellationToken cancellationToken)
        {
            var token = await _skipTokenService.SkipAsync(
                sessionId,
                tokenId,
                cancellationToken);

            return Ok(token);
        }

        [HttpPost("session/{sessionId:guid}/token/{tokenId:guid}/complete")]
        public async Task<IActionResult> Complete(
            Guid sessionId,
            Guid tokenId,
            CancellationToken cancellationToken)
        {
            var token = await _completeTokenService.CompleteAsync(
                sessionId,
                tokenId,
                cancellationToken);

            return Ok(token);
        }

        [HttpPost("session/{sessionId:guid}/token/{tokenId:guid}/cancel")]
        public async Task<IActionResult> Cancel(
            Guid sessionId,
            Guid tokenId,
            CancellationToken cancellationToken)
        {
            var token = await _cancelTokenService.CancelAsync(
                sessionId,
                tokenId,
                cancellationToken);

            return Ok(token);
        }

        [HttpPost("session/{sessionId:guid}/token/{tokenId:guid}/late")]
        public async Task<IActionResult> MarkLate(
            Guid sessionId,
            Guid tokenId,
            CancellationToken cancellationToken)
        {
            var token = await _markTokenLateService.MarkLateAsync(
                sessionId,
                tokenId,
                cancellationToken);

            return Ok(token);
        }

        [HttpGet("session/{sessionId:guid}/token/{tokenId:guid}/position")]
        public async Task<IActionResult> GetPosition(
            Guid sessionId,
            Guid tokenId,
            CancellationToken cancellationToken)
        {
            var position = await _getQueuePositionService.GetAsync(
                sessionId,
                tokenId,
                cancellationToken);

            return Ok(position);
        }

        [HttpGet("session/{sessionId:guid}/token/{tokenId:guid}/estimated-wait")]
        public async Task<IActionResult> GetEstimatedWait(
            Guid sessionId,
            Guid tokenId,
            CancellationToken cancellationToken)
        {
            var estimate = await _getEstimatedWaitService.GetAsync(
                sessionId,
                tokenId,
                cancellationToken);

            return Ok(estimate);
        }
    }
}

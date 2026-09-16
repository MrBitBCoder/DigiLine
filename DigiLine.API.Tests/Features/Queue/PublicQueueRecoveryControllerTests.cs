using DigiLine.API.Features.Queue;
using DigiLine.Application.Queue;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace DigiLine.API.Tests;

public sealed class PublicQueueRecoveryControllerTests
{
    [Fact]
    public async Task Recover_ReturnsExistingTokenWithoutUsingBookingContract()
    {
        var expected = new PublicTokenRecoveryResult(Guid.NewGuid(), Guid.NewGuid(), 15);
        var controller = new PublicQueueController(new Stub(expected), new CancelStub());
        var result = await controller.Recover(new PublicRecoveryRequest(expected.SessionId, Guid.NewGuid()), "access", default);
        Assert.Same(expected, Assert.IsType<OkObjectResult>(result).Value);
    }

    [Fact]
    public async Task Recover_MissingOrAmbiguousMatchUsesTheSameSafeNotFoundResponse()
    {
        var controller = new PublicQueueController(new Stub(null), new CancelStub());
        var result = Assert.IsType<ObjectResult>(await controller.Recover(new PublicRecoveryRequest(Guid.NewGuid(), Guid.NewGuid()), "access", default));
        Assert.Equal(404, result.StatusCode);
        var problem = Assert.IsType<ProblemDetails>(result.Value);
        Assert.Equal("We couldn't find an active token for this patient in this session.", problem.Detail);
    }

    [Fact]
    public async Task CancelToken_ReturnsUpdatedPatientState()
    {
        var sessionId = Guid.NewGuid();
        var tokenId = Guid.NewGuid();
        var expected = new PublicTokenState(sessionId, tokenId, 15,
            DigiLine.Domain.Common.QueueTokenStatus.Cancelled, 4, null, null, null,
            false, DigiLine.Domain.Common.SessionStatus.Running, DateTime.UtcNow);
        var controller = new PublicQueueController(new Stub(null), new CancelStub(expected));

        var result = await controller.CancelToken(sessionId, tokenId, "access", default);

        Assert.Same(expected, Assert.IsType<OkObjectResult>(result).Value);
    }

    [Fact]
    public async Task CancelToken_MissingOrUnownedTokenUsesSafeNotFoundResponse()
    {
        var controller = new PublicQueueController(new Stub(null), new CancelStub());

        var result = Assert.IsType<ObjectResult>(await controller.CancelToken(
            Guid.NewGuid(), Guid.NewGuid(), "access", default));

        Assert.Equal(404, result.StatusCode);
        var problem = Assert.IsType<ProblemDetails>(result.Value);
        Assert.Equal("We couldn't find this token for your verified patient account.", problem.Detail);
    }

    [Fact]
    public void CancelToken_UsesDedicatedPatientActionRateLimit()
    {
        var attribute = typeof(PublicQueueController)
            .GetMethod(nameof(PublicQueueController.CancelToken))!
            .GetCustomAttributes(typeof(EnableRateLimitingAttribute), inherit: true)
            .Cast<EnableRateLimitingAttribute>()
            .Single();

        Assert.Equal("patient-token-action", attribute.PolicyName);
    }

    private sealed class Stub(PublicTokenRecoveryResult? result) : IPublicQueueService
    {
        public Task<PublicTokenRecoveryResult?> RecoverAsync(string accessToken, Guid sessionId, Guid patientProfileId, CancellationToken cancellationToken = default) => Task.FromResult(result);
        public Task<PublicBookingResult> BookAsync(string accessToken, Guid sessionId, Guid patientProfileId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<PublicTokenState?> GetTokenAsync(string accessToken, Guid sessionId, Guid tokenId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }

    private sealed class CancelStub(PublicTokenState? result = null) : ICancelPatientTokenService
    {
        public Task<PublicTokenState?> CancelAsync(string accessToken, Guid sessionId, Guid tokenId,
            CancellationToken cancellationToken = default) => Task.FromResult(result);
    }
}

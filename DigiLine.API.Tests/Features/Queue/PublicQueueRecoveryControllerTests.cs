using DigiLine.API.Features.Queue;
using DigiLine.Application.Queue;
using Microsoft.AspNetCore.Mvc;

namespace DigiLine.API.Tests;

public sealed class PublicQueueRecoveryControllerTests
{
    [Fact]
    public async Task Recover_ReturnsExistingTokenWithoutUsingBookingContract()
    {
        var expected = new PublicTokenRecoveryResult(Guid.NewGuid(), Guid.NewGuid(), 15);
        var controller = new PublicQueueController(new Stub(expected));
        var result = await controller.Recover(new PublicRecoveryRequest(expected.SessionId, Guid.NewGuid()), "access", default);
        Assert.Same(expected, Assert.IsType<OkObjectResult>(result).Value);
    }

    [Fact]
    public async Task Recover_MissingOrAmbiguousMatchUsesTheSameSafeNotFoundResponse()
    {
        var controller = new PublicQueueController(new Stub(null));
        var result = Assert.IsType<ObjectResult>(await controller.Recover(new PublicRecoveryRequest(Guid.NewGuid(), Guid.NewGuid()), "access", default));
        Assert.Equal(404, result.StatusCode);
        var problem = Assert.IsType<ProblemDetails>(result.Value);
        Assert.Equal("We couldn't find an active token for this patient in this session.", problem.Detail);
    }

    private sealed class Stub(PublicTokenRecoveryResult? result) : IPublicQueueService
    {
        public Task<PublicTokenRecoveryResult?> RecoverAsync(string accessToken, Guid sessionId, Guid patientProfileId, CancellationToken cancellationToken = default) => Task.FromResult(result);
        public Task<PublicBookingResult> BookAsync(string accessToken, Guid sessionId, Guid patientProfileId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<PublicTokenState?> GetTokenAsync(string accessToken, Guid sessionId, Guid tokenId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }
}

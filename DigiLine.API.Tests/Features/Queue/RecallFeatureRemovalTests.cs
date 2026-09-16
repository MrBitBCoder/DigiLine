using DigiLine.API.Features.Queue;
using DigiLine.API.Features.Reception;
using DigiLine.API.Features.Sessions;
using DigiLine.Application.Queue;
using DigiLine.Domain.Entities;

namespace DigiLine.API.Tests;

public sealed class RecallFeatureRemovalTests
{
    [Fact]
    public void RecallCommandsAndEndpointsAreNotExposed()
    {
        Assert.Null(typeof(QueueToken).GetMethod("Recall"));
        Assert.Null(typeof(SessionsController).GetMethod("Recall"));
        Assert.Null(typeof(ReceptionController).GetMethod("Recall"));
        Assert.Null(typeof(QueueController).GetMethod("Recall"));
        Assert.DoesNotContain("TokenRecalled", Enum.GetNames<QueueEventType>());
    }
}

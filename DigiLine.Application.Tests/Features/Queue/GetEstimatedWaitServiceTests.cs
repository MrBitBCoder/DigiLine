using DigiLine.Application.Queue;
using DigiLine.Application.Sessions;
using DigiLine.Domain.Common;
using DigiLine.Domain.Entities;

namespace DigiLine.Application.Tests;

public class GetEstimatedWaitServiceTests
{
    [Fact]
    public async Task GetAsync_MultipliesPatientsAheadBySessionAverage()
    {
        var session = CreateSession(8);
        var tokenId = Guid.NewGuid();
        var positionService = new StubPositionService(
            new QueuePositionResult(
                tokenId, 18, QueueTokenStatus.Waiting, 6, 5));
        var service = new GetEstimatedWaitService(
            new StubSessionRepository(session),
            positionService);

        var result = await service.GetAsync(session.Id, tokenId);

        Assert.Equal(tokenId, result.TokenId);
        Assert.Equal(18, result.TokenNumber);
        Assert.Equal(6, result.Position);
        Assert.Equal(5, result.PatientsAhead);
        Assert.Equal(8, result.AverageConsultationMinutes);
        Assert.Equal(40, result.EstimatedWaitMinutes);
    }

    [Fact]
    public async Task GetAsync_WhenFirstInQueue_ReturnsZeroMinutes()
    {
        var session = CreateSession(10);
        var tokenId = Guid.NewGuid();
        var service = new GetEstimatedWaitService(
            new StubSessionRepository(session),
            new StubPositionService(new QueuePositionResult(
                tokenId, 1, QueueTokenStatus.Waiting, 1, 0)));

        var result = await service.GetAsync(session.Id, tokenId);

        Assert.Equal(0, result.EstimatedWaitMinutes);
    }

    [Fact]
    public async Task GetAsync_PreservesLateTokenPositionData()
    {
        var session = CreateSession(12);
        var tokenId = Guid.NewGuid();
        var service = new GetEstimatedWaitService(
            new StubSessionRepository(session),
            new StubPositionService(new QueuePositionResult(
                tokenId, 3, QueueTokenStatus.Late, 4, 3)));

        var result = await service.GetAsync(session.Id, tokenId);

        Assert.Equal(QueueTokenStatus.Late, result.Status);
        Assert.Equal(36, result.EstimatedWaitMinutes);
    }

    [Fact]
    public async Task GetAsync_WhenSessionIsPaused_ReturnsUnavailableEstimate()
    {
        var session = CreateSession(10);
        session.OpenBooking();
        session.Start();
        session.Pause();
        var tokenId = Guid.NewGuid();
        var service = new GetEstimatedWaitService(
            new StubSessionRepository(session),
            new StubPositionService(new QueuePositionResult(
                tokenId, 3, QueueTokenStatus.Waiting, 3, 2)));

        var result = await service.GetAsync(session.Id, tokenId);

        Assert.True(result.IsPaused);
        Assert.Null(result.EstimatedWaitMinutes);
        Assert.Equal(2, result.PatientsAhead);
    }

    [Fact]
    public async Task GetAsync_AfterSessionResumes_RestoresNumericEstimate()
    {
        var session = CreateSession(10);
        session.OpenBooking();
        session.Start();
        session.Pause();
        session.Resume();
        var tokenId = Guid.NewGuid();
        var service = new GetEstimatedWaitService(
            new StubSessionRepository(session),
            new StubPositionService(new QueuePositionResult(
                tokenId, 3, QueueTokenStatus.Waiting, 3, 2)));

        var result = await service.GetAsync(session.Id, tokenId);

        Assert.False(result.IsPaused);
        Assert.Equal(20, result.EstimatedWaitMinutes);
    }

    [Fact]
    public async Task GetAsync_WhenSessionDoesNotExist_ThrowsBeforePositionLookup()
    {
        var positionService = new StubPositionService(null);
        var service = new GetEstimatedWaitService(
            new StubSessionRepository(null),
            positionService);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.GetAsync(Guid.NewGuid(), Guid.NewGuid()));

        Assert.Equal("Session was not found.", exception.Message);
        Assert.False(positionService.WasCalled);
    }

    [Fact]
    public async Task GetAsync_PropagatesPositionValidationFailure()
    {
        var session = CreateSession(10);
        var positionService = new StubPositionService(null)
        {
            Exception = new InvalidOperationException(
                "Queue position is only available for waiting or late tokens.")
        };
        var service = new GetEstimatedWaitService(
            new StubSessionRepository(session),
            positionService);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.GetAsync(session.Id, Guid.NewGuid()));

        Assert.Equal(
            "Queue position is only available for waiting or late tokens.",
            exception.Message);
    }

    private static Session CreateSession(int averageConsultationMinutes)
    {
        var now = DateTime.UtcNow;
        return new Session(
            Guid.NewGuid(), Guid.NewGuid(), "Estimate test",
            now.AddMinutes(-30), now.AddMinutes(30),
            now.AddHours(2), now.AddHours(1),
            averageConsultationMinutes);
    }

    private sealed class StubSessionRepository : ISessionRepository
    {
        private readonly Session? _session;
        public StubSessionRepository(Session? session) => _session = session;
        public Task<Session?> GetByIdAsync(Guid sessionId, CancellationToken cancellationToken = default) =>
            Task.FromResult(_session?.Id == sessionId ? _session : null);
        public Task AddAsync(Session session, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task SaveChangesAsync(CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }

    private sealed class StubPositionService : IGetQueuePositionService
    {
        private readonly QueuePositionResult? _result;
        public StubPositionService(QueuePositionResult? result) => _result = result;
        public bool WasCalled { get; private set; }
        public Exception? Exception { get; set; }

        public Task<QueuePositionResult> GetAsync(
            Guid sessionId,
            Guid tokenId,
            CancellationToken cancellationToken = default)
        {
            WasCalled = true;

            if (Exception is not null)
            {
                throw Exception;
            }

            return Task.FromResult(_result!);
        }
    }
}

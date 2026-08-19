using DigiLine.Application.Queue;
using DigiLine.Application.Sessions;
using DigiLine.Domain.Entities;

namespace DigiLine.Application.Tests;

public class GetOrderedQueueServiceTests
{
    [Fact]
    public async Task GetAsync_ReturnsOnlySessionTokensInTokenNumberOrder()
    {
        var session = CreateSession();
        var otherSession = CreateSession();
        var patientProfileId = Guid.NewGuid();
        var repository = new StubQueueTokenRepository(
            new QueueToken(session.Id, 3, "Third Walk-in", null),
            new QueueToken(otherSession.Id, 1, "Other Session", null),
            new QueueToken(session.Id, 1, patientProfileId),
            new QueueToken(session.Id, 2, "Second Walk-in", "12345"));
        var service = new GetOrderedQueueService(
            new StubSessionRepository(session),
            repository);

        var result = await service.GetAsync(session.Id);

        Assert.Equal(new[] { 1, 2, 3 }, result.Select(item => item.TokenNumber));
        Assert.Equal(patientProfileId, result[0].PatientProfileId);
        Assert.Null(result[0].WalkInName);
        Assert.Null(result[1].PatientProfileId);
        Assert.Equal("Second Walk-in", result[1].WalkInName);
        Assert.Equal("12345", result[1].PhoneNumber);
        Assert.DoesNotContain(result, item => item.WalkInName == "Other Session");
    }

    [Fact]
    public async Task GetAsync_WhenQueueIsEmpty_ReturnsEmptyList()
    {
        var session = CreateSession();
        var service = new GetOrderedQueueService(
            new StubSessionRepository(session),
            new StubQueueTokenRepository());

        var result = await service.GetAsync(session.Id);

        Assert.Empty(result);
    }

    [Fact]
    public async Task GetAsync_WhenSessionDoesNotExist_ThrowsBeforeReadingQueue()
    {
        var repository = new StubQueueTokenRepository();
        var service = new GetOrderedQueueService(
            new StubSessionRepository(null),
            repository);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.GetAsync(Guid.NewGuid()));

        Assert.Equal("Session was not found.", exception.Message);
        Assert.False(repository.WasRead);
    }

    private static Session CreateSession()
    {
        var now = DateTime.UtcNow;
        return new Session(
            Guid.NewGuid(),
            Guid.NewGuid(),
            "Queue ordering test",
            now.AddMinutes(-30),
            now.AddMinutes(30),
            now.AddHours(2),
            now.AddHours(1),
            10);
    }

    private sealed class StubSessionRepository : ISessionRepository
    {
        private readonly Session? _session;

        public StubSessionRepository(Session? session)
        {
            _session = session;
        }

        public Task<Session?> GetByIdAsync(
            Guid sessionId,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(_session?.Id == sessionId ? _session : null);

        public Task AddAsync(
            Session session,
            CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public Task SaveChangesAsync(
            CancellationToken cancellationToken = default) =>
            Task.CompletedTask;
    }

    private sealed class StubQueueTokenRepository : IQueueTokenRepository
    {
        private readonly IReadOnlyList<QueueToken> _tokens;

        public StubQueueTokenRepository(params QueueToken[] tokens)
        {
            _tokens = tokens;
        }

        public bool WasRead { get; private set; }

        public Task<IReadOnlyList<QueueToken>> GetBySessionOrderedAsync(
            Guid sessionId,
            CancellationToken cancellationToken = default)
        {
            WasRead = true;
            return Task.FromResult<IReadOnlyList<QueueToken>>(
                _tokens
                    .Where(token => token.SessionId == sessionId)
                    .ToList());
        }

        public Task<QueueToken?> GetNextWaitingForUpdateAsync(
            Guid sessionId,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<QueueToken?> GetByIdAsync(
            Guid sessionId,
            Guid tokenId,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<int?> GetMaximumTokenNumberAsync(
            Guid sessionId,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<int?> AllocateNextTokenNumberAsync(
            Guid sessionId,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task AddAsync(
            QueueToken queueToken,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task SaveChangesAsync(
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
    }
}

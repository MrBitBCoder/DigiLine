using System.Collections.Concurrent;
using DigiLine.Application.Queue;
using DigiLine.Application.Sessions;
using DigiLine.Application.Transactions;
using DigiLine.Domain.Common;
using DigiLine.Domain.Entities;

namespace DigiLine.Application.Tests;

public class CompleteAndCallNextTokenServiceTests
{
    [Fact]
    public async Task CompleteAndCallNext_CompletesCurrentAndCallsNextInQueueOrder()
    {
        var session = RunningSession();
        var current = Called(session.Id, 1);
        var later = new QueueToken(session.Id, 3, "Later", null); later.AssignQueueOrder(3);
        var next = new QueueToken(session.Id, 2, "Next", null); next.AssignQueueOrder(2);
        var fixture = new Fixture(session, current, later, next);

        var result = await fixture.Service.CompleteAndCallNextAsync(session.Id, current.Id);

        Assert.Equal(QueueTokenStatus.Completed, current.Status);
        Assert.Equal(current.Id, result.CompletedToken.Id);
        Assert.Equal(next.Id, result.CalledToken?.Id);
        Assert.Equal(QueueTokenStatus.Called, next.Status);
        Assert.Equal(QueueTokenStatus.Waiting, later.Status);
        Assert.Equal(1, fixture.Repository.SaveCount);
        Assert.True(fixture.Transactions.Items.Single().Committed);
        var queueEvent = Assert.Single(fixture.Publisher.Events);
        Assert.Equal(QueueEventType.TokenCompletedAndNextCalled, queueEvent.Type);
        Assert.Equal(next.Id, queueEvent.TokenId);
    }

    [Fact]
    public async Task CompleteAndCallNext_WhenCurrentIsLast_CompletesWithoutFakeNextToken()
    {
        var session = RunningSession();
        var current = Called(session.Id, 9);
        var fixture = new Fixture(session, current);

        var result = await fixture.Service.CompleteAndCallNextAsync(session.Id, current.Id);

        Assert.Equal(QueueTokenStatus.Completed, current.Status);
        Assert.Null(result.CalledToken);
        Assert.True(fixture.Transactions.Items.Single().Committed);
    }

    [Fact]
    public async Task CompleteAndCallNext_PreservesLatePatientQueueOrder()
    {
        var session = RunningSession();
        var current = Called(session.Id, 1);
        var waiting = new QueueToken(session.Id, 2, "Waiting", null); waiting.AssignQueueOrder(4);
        var late = Called(session.Id, 3); late.Skip(); late.MarkLate(); late.AssignQueueOrder(3);
        var fixture = new Fixture(session, waiting, current, late);

        var result = await fixture.Service.CompleteAndCallNextAsync(session.Id, current.Id);

        Assert.Equal(late.Id, result.CalledToken?.Id);
        Assert.Equal(QueueTokenStatus.Called, late.Status);
        Assert.Equal(QueueTokenStatus.Waiting, waiting.Status);
    }

    [Fact]
    public async Task CompleteAndCallNext_WithStaleCurrentId_DoesNotMutateQueue()
    {
        var session = RunningSession();
        var current = Called(session.Id, 1);
        var waiting = new QueueToken(session.Id, 2, "Waiting", null);
        var fixture = new Fixture(session, current, waiting);

        var error = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            fixture.Service.CompleteAndCallNextAsync(session.Id, Guid.NewGuid()));

        Assert.Equal("The selected token is no longer the current patient.", error.Message);
        Assert.Equal(QueueTokenStatus.Called, current.Status);
        Assert.Equal(QueueTokenStatus.Waiting, waiting.Status);
        Assert.Equal(0, fixture.Repository.SaveCount);
        Assert.False(fixture.Transactions.Items.Single().Committed);
    }

    [Fact]
    public async Task ConcurrentCombinedRequests_CannotCallTwoCurrentPatients()
    {
        var session = RunningSession();
        var current = Called(session.Id, 1);
        var second = new QueueToken(session.Id, 2, "Second", null);
        var third = new QueueToken(session.Id, 3, "Third", null);
        var fixture = new Fixture(session, current, second, third);

        var attempts = await Task.WhenAll(Enumerable.Range(0, 2).Select(async _ =>
        {
            try { await fixture.Service.CompleteAndCallNextAsync(session.Id, current.Id); return true; }
            catch (InvalidOperationException) { return false; }
        }));

        Assert.Single(attempts, success => success);
        Assert.Equal(QueueTokenStatus.Completed, current.Status);
        Assert.Single(new[] { second, third }, token => token.Status == QueueTokenStatus.Called);
        Assert.Single(new[] { second, third }, token => token.Status == QueueTokenStatus.Waiting);
    }

    private static QueueToken Called(Guid sessionId, int number)
    {
        var token = new QueueToken(sessionId, number, $"Patient {number}", null);
        token.Call(DateTime.UtcNow);
        return token;
    }

    private static Session RunningSession()
    {
        var now = DateTime.UtcNow;
        var session = new Session(Guid.NewGuid(), Guid.NewGuid(), "Queue", now.AddHours(-2),
            now.AddHours(-1), now.AddHours(1), now.AddMinutes(30), 10);
        session.OpenBooking(); session.Start();
        return session;
    }

    private sealed class Fixture
    {
        public Fixture(Session session, params QueueToken[] tokens)
        {
            Repository = new StubQueueRepository(tokens);
            Transactions = new SerialTransactions();
            Publisher = new RecordingPublisher(Transactions);
            Service = new CompleteAndCallNextTokenService(new StubSessions(session), Repository, Transactions, Publisher);
        }
        public StubQueueRepository Repository { get; }
        public SerialTransactions Transactions { get; }
        public RecordingPublisher Publisher { get; }
        public CompleteAndCallNextTokenService Service { get; }
    }

    private sealed class StubSessions(Session session) : ISessionRepository
    {
        public Task<Session?> GetByIdAsync(Guid id, CancellationToken ct = default) =>
            Task.FromResult<Session?>(session.Id == id ? session : null);
        public Task AddAsync(Session value, CancellationToken ct = default) => throw new NotSupportedException();
        public Task SaveChangesAsync(CancellationToken ct = default) => throw new NotSupportedException();
    }

    private sealed class StubQueueRepository(IReadOnlyList<QueueToken> tokens) : IQueueTokenRepository
    {
        public int SaveCount { get; private set; }
        public Task<QueueToken?> GetCurrentForUpdateAsync(Guid sessionId, CancellationToken ct = default) =>
            Task.FromResult(tokens.SingleOrDefault(token => token.SessionId == sessionId
                && (token.Status is QueueTokenStatus.Called or QueueTokenStatus.InConsultation)));
        public Task<QueueToken?> GetNextWaitingForUpdateAsync(Guid sessionId, CancellationToken ct = default) =>
            Task.FromResult(tokens.Where(token => token.SessionId == sessionId
                    && (token.Status is QueueTokenStatus.Waiting or QueueTokenStatus.Late))
                .OrderBy(token => token.QueueOrder).ThenBy(token => token.TokenNumber).FirstOrDefault());
        public Task SaveChangesAsync(CancellationToken ct = default) { SaveCount++; return Task.CompletedTask; }
        public Task<IReadOnlyList<QueueToken>> GetBySessionOrderedAsync(Guid sessionId, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<QueueToken?> GetByIdAsync(Guid sessionId, Guid tokenId, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<int?> GetMaximumTokenNumberAsync(Guid sessionId, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<int?> AllocateNextTokenNumberAsync(Guid sessionId, CancellationToken ct = default) => throw new NotSupportedException();
        public Task AddAsync(QueueToken token, CancellationToken ct = default) => throw new NotSupportedException();
    }

    private sealed class SerialTransactions : ITransactionManager
    {
        private readonly SemaphoreSlim _gate = new(1, 1);
        public ConcurrentBag<Transaction> Items { get; } = [];
        public async Task<ITransaction> BeginAsync(CancellationToken ct = default)
        {
            await _gate.WaitAsync(ct);
            var transaction = new Transaction(_gate); Items.Add(transaction); return transaction;
        }
    }

    private sealed class RecordingPublisher(SerialTransactions transactions) : IQueueEventPublisher
    {
        public ConcurrentBag<QueueChangedEvent> Events { get; } = [];
        public Task PublishAsync(QueueChangedEvent queueEvent, CancellationToken ct = default)
        {
            Assert.Contains(transactions.Items, transaction => transaction.Committed);
            Events.Add(queueEvent);
            return Task.CompletedTask;
        }
    }

    private sealed class Transaction(SemaphoreSlim gate) : ITransaction
    {
        private bool _disposed;
        public bool Committed { get; private set; }
        public Task CommitAsync(CancellationToken ct = default) { Committed = true; return Task.CompletedTask; }
        public Task RollbackAsync(CancellationToken ct = default) => Task.CompletedTask;
        public ValueTask DisposeAsync() { if (!_disposed) { _disposed = true; gate.Release(); } return ValueTask.CompletedTask; }
    }
}

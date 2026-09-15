using System.Collections.Concurrent;
using DigiLine.Application.Queue;
using DigiLine.Application.Sessions;
using DigiLine.Application.Transactions;
using DigiLine.Domain.Common;
using DigiLine.Domain.Entities;

namespace DigiLine.Application.Tests;

public class CallNextTokenServiceTests
{
    [Fact]
    public async Task CallAsync_CallsLowestNumberedWaitingToken()
    {
        var session = CreateRunningSession();
        var alreadyCalled = new QueueToken(session.Id, 1, "Already called", null);
        alreadyCalled.Call(DateTime.UtcNow.AddMinutes(-1));
        var second = new QueueToken(session.Id, 2, "Second", null);
        var third = new QueueToken(session.Id, 3, Guid.NewGuid());
        var fixture = new Fixture(session, third, alreadyCalled, second);

        var result = await fixture.Service.CallAsync(session.Id);

        Assert.Equal(second.Id, result.Id);
        Assert.Equal(2, result.TokenNumber);
        Assert.Equal(QueueTokenStatus.Called, result.Status);
        Assert.NotNull(result.CalledAt);
        Assert.Equal(QueueTokenStatus.Called, second.Status);
        Assert.NotNull(second.CalledAt);
        Assert.Equal(QueueTokenStatus.Waiting, third.Status);
        Assert.True(fixture.Transactions.Transactions.Single().Committed);
    }

    [Fact]
    public async Task CallAsync_WhenSessionDoesNotExist_ThrowsBeforeTransaction()
    {
        var fixture = new Fixture(null);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            fixture.Service.CallAsync(Guid.NewGuid()));

        Assert.Equal("Session was not found.", exception.Message);
        Assert.False(fixture.Transactions.Transactions.Single().Committed);
    }

    [Fact]
    public async Task CallAsync_WhenSessionIsNotRunning_RejectsCall()
    {
        var session = CreateScheduledSession();
        var fixture = new Fixture(
            session,
            new QueueToken(session.Id, 1, "Waiting", null));

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            fixture.Service.CallAsync(session.Id));

        Assert.Equal(
            "Call Next is only available for a running session.",
            exception.Message);
        Assert.False(fixture.Transactions.Transactions.Single().Committed);
    }

    [Fact]
    public async Task CallAsync_WhenSessionIsPaused_RejectsCall()
    {
        var session = CreateRunningSession();
        session.Pause();
        var fixture = new Fixture(
            session,
            new QueueToken(session.Id, 1, "Waiting", null));

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            fixture.Service.CallAsync(session.Id));

        Assert.Equal(
            "Call Next is only available for a running session.",
            exception.Message);
        Assert.False(fixture.Transactions.Transactions.Single().Committed);
    }

    [Fact]
    public async Task CallAsync_AfterSessionResumes_CallsNextToken()
    {
        var session = CreateRunningSession();
        session.Pause();
        session.Resume();
        var waiting = new QueueToken(session.Id, 1, "Waiting", null);
        var fixture = new Fixture(session, waiting);

        var result = await fixture.Service.CallAsync(session.Id);

        Assert.Equal(waiting.Id, result.Id);
        Assert.Equal(QueueTokenStatus.Called, result.Status);
    }

    [Fact]
    public async Task CallAsync_WhenNoWaitingTokens_DoesNotCommit()
    {
        var session = CreateRunningSession();
        var fixture = new Fixture(session);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            fixture.Service.CallAsync(session.Id));

        Assert.Equal(
            "There are no callable tokens in this session.",
            exception.Message);
        Assert.False(fixture.Transactions.Transactions.Single().Committed);
    }

    [Fact]
    public async Task CallAsync_WhenPersistenceFails_DoesNotCommit()
    {
        var session = CreateRunningSession();
        var token = new QueueToken(session.Id, 1, "Waiting", null);
        var fixture = new Fixture(session, true, token);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            fixture.Service.CallAsync(session.Id));

        Assert.False(fixture.Transactions.Transactions.Single().Committed);
        Assert.True(fixture.Transactions.Transactions.Single().Disposed);
    }

    [Fact]
    public async Task CallAsync_WhenCalledConcurrently_CallsEachTokenOnceInOrder()
    {
        const int count = 10;
        var session = CreateRunningSession();
        var tokens = Enumerable.Range(1, count)
            .Select(number => new QueueToken(
                session.Id,
                number,
                $"Walk-in {number}",
                null))
            .ToArray();
        var fixture = new Fixture(session, tokens);

        var results = await Task.WhenAll(
            Enumerable.Range(0, count)
                .Select(_ => fixture.Service.CallAsync(session.Id)));

        Assert.Equal(
            Enumerable.Range(1, count),
            results.Select(result => result.TokenNumber).OrderBy(number => number));
        Assert.Equal(count, results.Select(result => result.Id).Distinct().Count());
        Assert.All(tokens, token =>
            Assert.Equal(QueueTokenStatus.Called, token.Status));
    }

    [Fact]
    public async Task CallAsync_IgnoresSkippedToken()
    {
        var session = CreateRunningSession();
        var skipped = new QueueToken(session.Id, 1, "Skipped", null);
        skipped.Call(DateTime.UtcNow);
        skipped.Skip();
        var waiting = new QueueToken(session.Id, 2, "Waiting", null);
        var fixture = new Fixture(session, skipped, waiting);

        var result = await fixture.Service.CallAsync(session.Id);

        Assert.Equal(waiting.Id, result.Id);
        Assert.Equal(2, result.TokenNumber);
        Assert.Equal(QueueTokenStatus.Skipped, skipped.Status);
    }

    [Fact]
    public async Task CallAsync_IgnoresCompletedToken()
    {
        var session = CreateRunningSession();
        var completed = new QueueToken(session.Id, 1, "Completed", null);
        completed.Call(DateTime.UtcNow.AddMinutes(-5));
        completed.Complete(DateTime.UtcNow);
        var waiting = new QueueToken(session.Id, 2, "Waiting", null);
        var fixture = new Fixture(session, completed, waiting);

        var result = await fixture.Service.CallAsync(session.Id);

        Assert.Equal(waiting.Id, result.Id);
        Assert.Equal(2, result.TokenNumber);
        Assert.Equal(QueueTokenStatus.Completed, completed.Status);
    }

    [Fact]
    public async Task CallAsync_IgnoresCancelledToken()
    {
        var session = CreateRunningSession();
        var cancelled = new QueueToken(session.Id, 1, "Cancelled", null);
        cancelled.Cancel();
        var waiting = new QueueToken(session.Id, 2, "Waiting", null);
        var fixture = new Fixture(session, cancelled, waiting);

        var result = await fixture.Service.CallAsync(session.Id);

        Assert.Equal(waiting.Id, result.Id);
        Assert.Equal(2, result.TokenNumber);
        Assert.Equal(QueueTokenStatus.Cancelled, cancelled.Status);
    }

    [Fact]
    public async Task CallAsync_PrioritizesWaitingTokenAheadOfEarlierLateToken()
    {
        var session = CreateRunningSession();
        var late = new QueueToken(session.Id, 1, "Late", null);
        late.Call(DateTime.UtcNow.AddMinutes(-10));
        late.Skip();
        late.MarkLate();
        late.AssignQueueOrder(3);
        var waiting = new QueueToken(session.Id, 2, "Waiting", null);
        var fixture = new Fixture(session, late, waiting);

        var result = await fixture.Service.CallAsync(session.Id);

        Assert.Equal(waiting.Id, result.Id);
        Assert.Equal(QueueTokenStatus.Late, late.Status);
    }

    [Fact]
    public async Task CallAsync_WhenNoWaitingTokens_CallsLowestLateToken()
    {
        var session = CreateRunningSession();
        var laterLate = CreateLateToken(session.Id, 3);
        var earlierLate = CreateLateToken(session.Id, 1);
        var fixture = new Fixture(session, laterLate, earlierLate);

        var result = await fixture.Service.CallAsync(session.Id);

        Assert.Equal(earlierLate.Id, result.Id);
        Assert.Equal(QueueTokenStatus.Called, earlierLate.Status);
        Assert.Equal(QueueTokenStatus.Late, laterLate.Status);
    }

    [Fact]
    public async Task CallAsync_ArrivedPatientPrecedesLaterBooking()
    {
        var session = CreateRunningSession();
        var returned = CreateLateToken(session.Id, 1);
        returned.AssignQueueOrder(4);
        var first = new QueueToken(session.Id, 2, "First waiting", null);
        first.AssignQueueOrder(2);
        var second = new QueueToken(session.Id, 3, "Second waiting", null);
        second.AssignQueueOrder(3);
        var newcomer = new QueueToken(session.Id, 4, "New booking", null);
        newcomer.AssignQueueOrder(5);
        var fixture = new Fixture(session, newcomer, returned, second, first);

        Assert.Equal(first.Id, (await fixture.Service.CallAsync(session.Id)).Id);
        Assert.Equal(second.Id, (await fixture.Service.CallAsync(session.Id)).Id);
        Assert.Equal(returned.Id, (await fixture.Service.CallAsync(session.Id)).Id);
        Assert.Equal(newcomer.Id, (await fixture.Service.CallAsync(session.Id)).Id);
    }

    private static QueueToken CreateLateToken(Guid sessionId, int tokenNumber)
    {
        var token = new QueueToken(sessionId, tokenNumber, $"Late {tokenNumber}", null);
        token.Call(DateTime.UtcNow.AddMinutes(-10));
        token.Skip();
        token.MarkLate();
        return token;
    }

    private static Session CreateScheduledSession()
    {
        var now = DateTime.UtcNow;
        return new Session(
            Guid.NewGuid(),
            Guid.NewGuid(),
            "Call Next test",
            now.AddMinutes(-30),
            now.AddMinutes(-10),
            now.AddHours(2),
            now.AddHours(1),
            10);
    }

    private static Session CreateRunningSession()
    {
        var session = CreateScheduledSession();
        session.OpenBooking();
        session.Start();
        return session;
    }

    private sealed class Fixture
    {
        public Fixture(Session? session, params QueueToken[] tokens)
            : this(session, false, tokens)
        {
        }

        public Fixture(
            Session? session,
            bool throwOnSave,
            params QueueToken[] tokens)
        {
            Transactions = new SerialTransactionManager();
            Service = new CallNextTokenService(
                new StubSessionRepository(session),
                new StubQueueTokenRepository(tokens, throwOnSave),
                Transactions);
        }

        public SerialTransactionManager Transactions { get; }
        public CallNextTokenService Service { get; }
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
        private readonly bool _throwOnSave;

        public StubQueueTokenRepository(
            IReadOnlyList<QueueToken> tokens,
            bool throwOnSave)
        {
            _tokens = tokens;
            _throwOnSave = throwOnSave;
        }

        public Task<QueueToken?> GetNextWaitingForUpdateAsync(
            Guid sessionId,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(
                _tokens
                    .Where(token => token.SessionId == sessionId)
                    .Where(token => token.Status == QueueTokenStatus.Waiting
                        || token.Status == QueueTokenStatus.Late)
                    .OrderBy(token => token.QueueOrder)
                    .ThenBy(token => token.TokenNumber)
                    .FirstOrDefault());

        public Task<QueueToken?> GetByIdAsync(
            Guid sessionId,
            Guid tokenId,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task SaveChangesAsync(
            CancellationToken cancellationToken = default) =>
            _throwOnSave
                ? Task.FromException(new InvalidOperationException("Save failed."))
                : Task.CompletedTask;

        public Task<IReadOnlyList<QueueToken>> GetBySessionOrderedAsync(
            Guid sessionId,
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
    }

    private sealed class SerialTransactionManager : ITransactionManager
    {
        private readonly SemaphoreSlim _gate = new(1, 1);

        public ConcurrentBag<StubTransaction> Transactions { get; } = [];

        public async Task<ITransaction> BeginAsync(
            CancellationToken cancellationToken = default)
        {
            await _gate.WaitAsync(cancellationToken);
            var transaction = new StubTransaction(_gate);
            Transactions.Add(transaction);
            return transaction;
        }
    }

    private sealed class StubTransaction : ITransaction
    {
        private readonly SemaphoreSlim _gate;

        public StubTransaction(SemaphoreSlim gate)
        {
            _gate = gate;
        }

        public bool Committed { get; private set; }
        public bool Disposed { get; private set; }

        public Task CommitAsync(
            CancellationToken cancellationToken = default)
        {
            Committed = true;
            return Task.CompletedTask;
        }

        public Task RollbackAsync(
            CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public ValueTask DisposeAsync()
        {
            Disposed = true;
            _gate.Release();
            return ValueTask.CompletedTask;
        }
    }
}

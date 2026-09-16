using DigiLine.Application.Patients;
using DigiLine.Application.Queue;
using DigiLine.Application.Sessions;
using DigiLine.Application.Transactions;
using DigiLine.Domain.Common;
using DigiLine.Domain.Entities;

namespace DigiLine.Application.Tests;

public sealed class CancelPatientTokenServiceTests
{
    [Fact]
    public async Task CancelAsync_WhenOwnedAndWaiting_CancelsAfterSessionAndTokenLocks()
    {
        var session = RunningSession();
        var token = new QueueToken(session.Id, 15, Guid.NewGuid());
        var calls = new List<string>();
        var repository = new TokenRepository(token, calls);
        var transactions = new TransactionManager(calls);
        var publisher = new Publisher(transactions, repository);
        var now = new DateTime(2026, 9, 16, 8, 30, 0, DateTimeKind.Utc);
        var service = Service(session, repository, transactions, publisher, true, calls, now);

        var result = await service.CancelAsync("access", session.Id, token.Id);

        Assert.NotNull(result);
        Assert.Equal(QueueTokenStatus.Cancelled, result.Status);
        Assert.Equal(now, token.FinalizedAt);
        Assert.Equal(new[] { "begin", "session", "token", "save", "commit" }, calls.Take(5));
        Assert.Single(publisher.Events);
        Assert.True(publisher.PublishedAfterCommitAndSave);
    }

    [Fact]
    public async Task CancelAsync_WhenTokenIsNotOwned_ReturnsNothingWithoutMutation()
    {
        var session = RunningSession();
        var token = new QueueToken(session.Id, 4, Guid.NewGuid());
        var repository = new TokenRepository(token, []);
        var service = Service(session, repository, new TransactionManager([]), null, false, [], DateTime.UtcNow);

        var result = await service.CancelAsync("access", session.Id, token.Id);

        Assert.Null(result);
        Assert.Equal(QueueTokenStatus.Waiting, token.Status);
        Assert.False(repository.WasSaved);
    }

    [Fact]
    public async Task CancelAsync_WhenPatientAccessExpired_RejectsBeforeTransaction()
    {
        var session = RunningSession();
        var token = new QueueToken(session.Id, 4, Guid.NewGuid());
        var transactions = new TransactionManager([]);
        var service = new CancelPatientTokenService(new Access(false), new Patients(true),
            new Sessions(session, []), new TokenRepository(token, []), transactions);

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            service.CancelAsync("expired", session.Id, token.Id));

        Assert.False(transactions.Began);
    }

    [Fact]
    public async Task CancelAsync_WhenTokenWasCalled_ReturnsLifecycleConflictWithoutSaveOrEvent()
    {
        var session = RunningSession();
        var token = new QueueToken(session.Id, 4, Guid.NewGuid());
        token.Call(DateTime.UtcNow);
        var repository = new TokenRepository(token, []);
        var publisher = new Publisher(new TransactionManager([]), repository);
        var service = Service(session, repository, new TransactionManager([]), publisher, true, [], DateTime.UtcNow);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.CancelAsync("access", session.Id, token.Id));

        Assert.Equal("This token can no longer be cancelled.", exception.Message);
        Assert.False(repository.WasSaved);
        Assert.Empty(publisher.Events);
    }

    [Fact]
    public async Task CancelAsync_WhenSaveFails_DoesNotPublish()
    {
        var session = RunningSession();
        var token = new QueueToken(session.Id, 4, Guid.NewGuid());
        var repository = new TokenRepository(token, [], throwOnSave: true);
        var transactions = new TransactionManager([]);
        var publisher = new Publisher(transactions, repository);
        var service = Service(session, repository, transactions, publisher, true, [], DateTime.UtcNow);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.CancelAsync("access", session.Id, token.Id));

        Assert.Empty(publisher.Events);
        Assert.False(transactions.Transaction.Committed);
    }

    private static CancelPatientTokenService Service(Session session, TokenRepository tokens,
        TransactionManager transactions, Publisher? publisher, bool owns, List<string> calls, DateTime now) =>
        new(new Access(true), new Patients(owns), new Sessions(session, calls), tokens,
            transactions, publisher, new Clock(now));

    private static Session RunningSession()
    {
        var now = DateTime.UtcNow;
        var session = new Session(Guid.NewGuid(), Guid.NewGuid(), "Live", now.AddHours(-2),
            now.AddHours(-1), now.AddHours(1), now.AddMinutes(30), 10);
        session.OpenBooking();
        session.Start();
        return session;
    }

    private sealed class Access(bool valid) : IPatientAccessService
    {
        public string? GetPhoneNumber(string accessToken) => valid ? "9876543210" : null;
        public PhoneVerificationChallenge CreateChallenge(string phoneNumber) => throw new NotSupportedException();
        public string? Verify(Guid challengeId, string code) => throw new NotSupportedException();
        public string IssueAccess(string phoneNumber) => throw new NotSupportedException();
    }

    private sealed class Patients(bool owns) : IPublicPatientRepository
    {
        public Task<bool> IsOwnedByPhoneAsync(Guid profileId, string phoneNumber,
            CancellationToken cancellationToken = default) => Task.FromResult(owns);
        public Task<IReadOnlyList<PublicPatientProfile>> ListByPhoneAsync(string phoneNumber,
            CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<PublicPatientProfile> CreateAsync(string phoneNumber, string name, int age,
            CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }

    private sealed class Sessions(Session session, List<string> calls) : ISessionRepository
    {
        public Task<Session?> GetByIdAsync(Guid sessionId, CancellationToken cancellationToken = default)
        {
            calls.Add("session");
            return Task.FromResult<Session?>(session.Id == sessionId ? session : null);
        }
        public Task AddAsync(Session value, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task SaveChangesAsync(CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }

    private sealed class TokenRepository(QueueToken token, List<string> calls, bool throwOnSave = false)
        : IQueueTokenRepository
    {
        public bool WasSaved { get; private set; }
        public Task<QueueToken?> GetByIdForUpdateAsync(Guid sessionId, Guid tokenId,
            CancellationToken cancellationToken = default)
        {
            calls.Add("token");
            return Task.FromResult<QueueToken?>(token.SessionId == sessionId && token.Id == tokenId ? token : null);
        }
        public Task SaveChangesAsync(CancellationToken cancellationToken = default)
        {
            if (throwOnSave) throw new InvalidOperationException("Save failed.");
            calls.Add("save"); WasSaved = true; return Task.CompletedTask;
        }
        public Task<IReadOnlyList<QueueToken>> GetBySessionOrderedAsync(Guid sessionId,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<QueueToken>>(token.SessionId == sessionId ? [token] : []);
        public Task<QueueToken?> GetByIdAsync(Guid sessionId, Guid tokenId,
            CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<QueueToken?> GetNextWaitingForUpdateAsync(Guid sessionId,
            CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<int?> GetMaximumTokenNumberAsync(Guid sessionId,
            CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<int?> AllocateNextTokenNumberAsync(Guid sessionId,
            CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task AddAsync(QueueToken queueToken,
            CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }

    private sealed class TransactionManager(List<string> calls) : ITransactionManager
    {
        public Transaction Transaction { get; } = new(calls);
        public bool Began { get; private set; }
        public Task<ITransaction> BeginAsync(CancellationToken cancellationToken = default)
        {
            Began = true; calls.Add("begin"); return Task.FromResult<ITransaction>(Transaction);
        }
    }

    private sealed class Transaction(List<string> calls) : ITransaction
    {
        public bool Committed { get; private set; }
        public Task CommitAsync(CancellationToken cancellationToken = default)
        {
            calls.Add("commit"); Committed = true; return Task.CompletedTask;
        }
        public Task RollbackAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }

    private sealed class Publisher(TransactionManager transactions, TokenRepository tokens) : IQueueEventPublisher
    {
        public List<QueueChangedEvent> Events { get; } = [];
        public bool PublishedAfterCommitAndSave { get; private set; }
        public Task PublishAsync(QueueChangedEvent queueEvent,
            CancellationToken cancellationToken = default)
        {
            PublishedAfterCommitAndSave = transactions.Transaction.Committed && tokens.WasSaved;
            Events.Add(queueEvent); return Task.CompletedTask;
        }
    }

    private sealed class Clock(DateTime now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => new(now);
    }
}

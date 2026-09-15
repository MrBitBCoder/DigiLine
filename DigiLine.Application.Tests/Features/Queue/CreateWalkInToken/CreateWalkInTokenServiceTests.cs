using System.Collections.Concurrent;
using DigiLine.Application.Patients;
using DigiLine.Application.Queue;
using DigiLine.Application.Sessions;
using DigiLine.Application.Transactions;
using DigiLine.Domain.Entities;

namespace DigiLine.Application.Tests;

public class CreateWalkInTokenServiceTests
{
    [Fact]
    public async Task CreateAsync_PersistsRequiredReceptionAge()
    {
        var fixture = new Fixture(CreateOpenSession());
        await fixture.Service.CreateAsync(fixture.Session.Id, "Jane Doe", null, default, 34);
        Assert.Equal(34, Assert.Single(fixture.QueueTokenRepository.Tokens).WalkInAge);
    }

    [Fact]
    public async Task CreateAsync_RejectsOutOfRangeAge()
    {
        var fixture = new Fixture(CreateOpenSession());
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            fixture.Service.CreateAsync(fixture.Session.Id, "Jane Doe", null, default, 131));
        Assert.Empty(fixture.QueueTokenRepository.Tokens);
    }

    [Fact]
    public async Task CreateAsync_WithValidDetails_PersistsWalkInAndCommits()
    {
        var fixture = new Fixture(CreateOpenSession());

        var tokenNumber = await fixture.Service.CreateAsync(
            fixture.Session.Id,
            "  Jane Doe  ",
            "  9876543210  ");

        var token = Assert.Single(fixture.QueueTokenRepository.Tokens);
        Assert.Equal(1, tokenNumber);
        Assert.Equal("Jane Doe", token.WalkInName);
        Assert.Equal("9876543210", token.PhoneNumber);
        Assert.Null(token.PatientProfileId);
        Assert.True(fixture.TransactionManager.Transaction.Committed);
        Assert.True(fixture.TransactionManager.Transaction.Disposed);
    }

    [Fact]
    public async Task CreateAsync_WhenPersistenceFails_DoesNotCommitTransaction()
    {
        var fixture = new Fixture(
            CreateOpenSession(),
            throwOnSave: true);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            fixture.Service.CreateAsync(
                fixture.Session.Id,
                "Jane Doe",
                null));

        Assert.False(fixture.TransactionManager.Transaction.Committed);
        Assert.True(fixture.TransactionManager.Transaction.Disposed);
    }

    [Fact]
    public async Task CreateAsync_WhenSessionDoesNotExist_ThrowsBeforeTransaction()
    {
        var fixture = new Fixture(null);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            fixture.Service.CreateAsync(Guid.NewGuid(), "Jane Doe", null));

        Assert.Equal("Session was not found.", exception.Message);
        Assert.Equal(1, fixture.TransactionManager.BeginCount);
        Assert.False(fixture.TransactionManager.Transaction.Committed);
        Assert.Empty(fixture.QueueTokenRepository.Tokens);
    }

    [Fact]
    public async Task CreateAsync_WhenSessionIsScheduled_RejectsToken()
    {
        var now = DateTime.UtcNow;
        var session = new Session(Guid.NewGuid(), Guid.NewGuid(), "Unpublished",
            now.AddMinutes(-30), now.AddMinutes(30), now.AddHours(2), now.AddHours(1), 10);
        var fixture = new Fixture(session);

        var error = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            fixture.Service.CreateAsync(session.Id, "Jane Doe", null));

        Assert.Equal("Booking is not currently open for this session.", error.Message);
        Assert.Equal(1, fixture.TransactionManager.BeginCount);
        Assert.False(fixture.TransactionManager.Transaction.Committed);
    }

    [Fact]
    public async Task CreateAsync_WhenSessionIsClosed_RejectsToken()
    {
        var session = CreateOpenSession();
        session.Start();
        session.StopBooking(DateTime.UtcNow); session.CompleteSession(DateTime.UtcNow);
        var fixture = new Fixture(session);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            fixture.Service.CreateAsync(session.Id, "Jane Doe", null));

        Assert.Equal(
            "Cannot create a token for a closed session.",
            exception.Message);
        Assert.Equal(1, fixture.TransactionManager.BeginCount);
        Assert.False(fixture.TransactionManager.Transaction.Committed);
    }

    [Fact]
    public async Task CreateAsync_WhenBookingWindowIsClosed_RejectsToken()
    {
        var now = DateTime.UtcNow;
        var session = new Session(
            Guid.NewGuid(),
            Guid.NewGuid(),
            "Past session",
            now.AddHours(-3),
            now.AddHours(-2),
            now.AddHours(1),
            now.AddMinutes(-1),
            10);
        var fixture = new Fixture(session);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            fixture.Service.CreateAsync(session.Id, "Jane Doe", null));

        Assert.Equal(
            "Booking is not currently open for this session.",
            exception.Message);
        Assert.Equal(1, fixture.TransactionManager.BeginCount);
        Assert.False(fixture.TransactionManager.Transaction.Committed);
    }

    [Fact]
    public async Task CreateAsync_WhenWalkInsAreConcurrent_ReturnsUniqueConsecutiveNumbers()
    {
        const int count = 25;
        var fixture = new Fixture(CreateOpenSession());

        var tasks = Enumerable.Range(1, count)
            .Select(index => fixture.Service.CreateAsync(
                fixture.Session.Id,
                $"Walk-in {index}",
                null));

        var numbers = await Task.WhenAll(tasks);

        Assert.Equal(
            Enumerable.Range(1, count),
            numbers.OrderBy(number => number));
        Assert.Equal(count, numbers.Distinct().Count());
        Assert.Equal(count, fixture.QueueTokenRepository.Tokens.Count);
    }

    [Fact]
    public async Task PatientBooking_WhenSessionIsScheduled_RejectsToken()
    {
        var now = DateTime.UtcNow;
        var session = new Session(Guid.NewGuid(), Guid.NewGuid(), "Unpublished",
            now.AddMinutes(-30), now.AddMinutes(30), now.AddHours(2), now.AddHours(1), 10);
        var patient = new PatientProfile(Guid.NewGuid(), "Registered Patient", 30);
        var transactions = new StubTransactionManager();
        var service = new BookTokenService(new AtomicTokenNumberService(),
            new InMemoryQueueTokenRepository(), new StubSessionRepository(session),
            new StubPatientProfileRepository(patient), transactions);

        var error = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.BookAsync(session.Id, patient.Id));

        Assert.Equal("Booking is not currently open for this session.", error.Message);
        Assert.Equal(1, transactions.BeginCount);
        Assert.False(transactions.Transaction.Committed);
    }

    [Fact]
    public async Task PatientAndWalkInBookings_UseSameTokenSequence()
    {
        var session = CreateOpenSession();
        var allocator = new AtomicTokenNumberService();
        var repository = new InMemoryQueueTokenRepository();
        var sessionRepository = new StubSessionRepository(session);
        var patient = new PatientProfile(
            Guid.NewGuid(),
            "Registered Patient",
            30);

        var bookService = new BookTokenService(
            allocator,
            repository,
            sessionRepository,
            new StubPatientProfileRepository(patient),
            new StubTransactionManager());
        var walkInService = new CreateWalkInTokenService(
            allocator,
            repository,
            sessionRepository,
            new StubTransactionManager());

        var first = await bookService.BookAsync(session.Id, patient.Id);
        var second = await walkInService.CreateAsync(
            session.Id,
            "Walk-in Patient",
            null);
        var third = await bookService.BookAsync(session.Id, patient.Id);

        Assert.Equal(new[] { 1, 2, 3 }, new[] { first, second, third });
        Assert.Equal(
            new[] { 1, 2, 3 },
            repository.Tokens
                .OrderBy(token => token.TokenNumber)
                .Select(token => token.TokenNumber));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task StoppedLiveSession_RejectsPatientAndWalkInBookings(bool paused)
    {
        var session = CreateOpenSession();
        session.Start();
        if (paused) session.Pause();
        session.StopBooking(DateTime.UtcNow);
        var repository = new InMemoryQueueTokenRepository();
        var transactions = new StubTransactionManager();
        var patient = new PatientProfile(Guid.NewGuid(), "Patient", 30);
        var service = new BookTokenService(new AtomicTokenNumberService(), repository,
            new StubSessionRepository(session), new StubPatientProfileRepository(patient), transactions);
        var error = await Assert.ThrowsAsync<InvalidOperationException>(() => service.BookAsync(session.Id, patient.Id));
        Assert.Equal("Booking is not currently open for this session.", error.Message);
        Assert.Empty(repository.Tokens);
        Assert.False(transactions.Transaction.Committed);
        var walkIn = new Fixture(session);
        await Assert.ThrowsAsync<InvalidOperationException>(() => walkIn.Service.CreateAsync(session.Id, "New patient", null));
        Assert.Empty(walkIn.QueueTokenRepository.Tokens);
    }

    private static Session CreateOpenSession()
    {
        var now = DateTime.UtcNow;
        var session = new Session(
            Guid.NewGuid(),
            Guid.NewGuid(),
            "Open session",
            now.AddMinutes(-30),
            now.AddMinutes(30),
            now.AddHours(2),
            now.AddHours(1),
            10);
        session.OpenBooking();
        return session;
    }

    private sealed class Fixture
    {
        public Fixture(Session? session, bool throwOnSave = false)
        {
            Session = session ?? CreateOpenSession();
            QueueTokenRepository = new InMemoryQueueTokenRepository(throwOnSave);
            TransactionManager = new StubTransactionManager();
            Service = new CreateWalkInTokenService(
                new AtomicTokenNumberService(),
                QueueTokenRepository,
                new StubSessionRepository(session),
                TransactionManager);
        }

        public Session Session { get; }
        public InMemoryQueueTokenRepository QueueTokenRepository { get; }
        public StubTransactionManager TransactionManager { get; }
        public CreateWalkInTokenService Service { get; }
    }

    private sealed class AtomicTokenNumberService : ITokenNumberService
    {
        private int _next;

        public Task<int> GetNextTokenNumberAsync(
            Guid sessionId,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(Interlocked.Increment(ref _next));
        }
    }

    private sealed class InMemoryQueueTokenRepository : IQueueTokenRepository
    {
        private readonly bool _throwOnSave;

        public InMemoryQueueTokenRepository(bool throwOnSave = false)
        {
            _throwOnSave = throwOnSave;
        }

        public ConcurrentBag<QueueToken> Tokens { get; } = [];

        public Task<IReadOnlyList<QueueToken>> GetBySessionOrderedAsync(
            Guid sessionId,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<QueueToken>>(
                Tokens
                    .Where(token => token.SessionId == sessionId)
                    .OrderBy(token => token.TokenNumber)
                    .ToList());

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
            Task.FromResult<int?>(null);

        public Task<int?> AllocateNextTokenNumberAsync(
            Guid sessionId,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<int?>(null);

        private long _nextQueueOrder;
        public Task<long?> AllocateNextQueueOrderAsync(Guid sessionId, CancellationToken cancellationToken = default) =>
            Task.FromResult<long?>(Interlocked.Increment(ref _nextQueueOrder));

        public Task AddAsync(
            QueueToken queueToken,
            CancellationToken cancellationToken = default)
        {
            Tokens.Add(queueToken);
            return Task.CompletedTask;
        }

        public Task SaveChangesAsync(
            CancellationToken cancellationToken = default)
        {
            return _throwOnSave
                ? Task.FromException(new InvalidOperationException("Save failed."))
                : Task.CompletedTask;
        }
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

    private sealed class StubPatientProfileRepository
        : IPatientProfileRepository
    {
        private readonly PatientProfile _patient;

        public StubPatientProfileRepository(PatientProfile patient)
        {
            _patient = patient;
        }

        public Task<PatientProfile?> GetByIdAsync(
            Guid patientProfileId,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(
                _patient.Id == patientProfileId ? _patient : null);

        public Task AddAsync(
            PatientProfile patientProfile,
            CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public Task SaveChangesAsync(
            CancellationToken cancellationToken = default) =>
            Task.CompletedTask;
    }

    private sealed class StubTransactionManager : ITransactionManager
    {
        public int BeginCount { get; private set; }
        public StubTransaction Transaction { get; } = new();

        public Task<ITransaction> BeginAsync(
            CancellationToken cancellationToken = default)
        {
            BeginCount++;
            return Task.FromResult<ITransaction>(Transaction);
        }
    }

    private sealed class StubTransaction : ITransaction
    {
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
            return ValueTask.CompletedTask;
        }
    }
}

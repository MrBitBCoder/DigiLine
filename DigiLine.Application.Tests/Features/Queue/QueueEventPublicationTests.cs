using DigiLine.Application.Queue;
using DigiLine.Application.Patients;
using DigiLine.Application.Sessions;
using DigiLine.Application.Transactions;
using DigiLine.Domain.Common;
using DigiLine.Domain.Entities;

namespace DigiLine.Application.Tests;

public class QueueEventPublicationTests
{
    [Fact]
    public async Task CreateWalkIn_PublishesCreatedEventAfterCommit()
    {
        var session = CreateScheduledSession();
        var queueRepository = new StubQueueTokenRepository();
        var transactionManager = new StubTransactionManager();
        var publisher = new RecordingPublisher(() =>
            queueRepository.WasSaved
            && transactionManager.Transaction.Committed);
        var service = new CreateWalkInTokenService(
            new StubTokenNumberService(7),
            queueRepository,
            new StubSessionRepository(session),
            transactionManager,
            publisher);

        await service.CreateAsync(session.Id, "Jane Doe", null);

        var queueEvent = Assert.Single(publisher.Events);
        Assert.Equal(QueueEventType.WalkInCreated, queueEvent.Type);
        Assert.Equal(session.Id, queueEvent.SessionId);
        Assert.Equal(7, queueEvent.TokenNumber);
        Assert.Equal(QueueTokenStatus.Waiting, queueEvent.TokenStatus);
    }

    [Fact]
    public async Task BookToken_PublishesBookedEventAfterCommit()
    {
        var session = CreateScheduledSession();
        var patient = new PatientProfile(Guid.NewGuid(), "Booked Patient", 30);
        var queueRepository = new StubQueueTokenRepository();
        var transactions = new StubTransactionManager();
        var publisher = new RecordingPublisher(() =>
            queueRepository.WasSaved && transactions.Transaction.Committed);
        var service = new BookTokenService(
            new StubTokenNumberService(12), queueRepository,
            new StubSessionRepository(session),
            new StubPatientProfileRepository(patient),
            transactions, publisher);

        await service.BookAsync(session.Id, patient.Id);

        var queueEvent = Assert.Single(publisher.Events);
        Assert.Equal(QueueEventType.TokenBooked, queueEvent.Type);
        Assert.Equal(session.Id, queueEvent.SessionId);
        Assert.Equal(12, queueEvent.TokenNumber);
    }

    [Fact]
    public async Task QueueTransitions_PublishEveryRequiredEventAfterSave()
    {
        var session = CreateRunningSession();
        var publisher = new RecordingPublisher();

        var next = new QueueToken(session.Id, 1, "Next", null);
        await new CallNextTokenService(
            new StubSessionRepository(session),
            new StubQueueTokenRepository(next),
            new StubTransactionManager(), publisher).CallAsync(session.Id);

        var recalled = new QueueToken(session.Id, 2, "Recall", null);
        recalled.Call(DateTime.UtcNow);
        await new RecallTokenService(new StubSessionRepository(session), new StubQueueTokenRepository(recalled), publisher).RecallAsync(session.Id, recalled.Id);

        var skipped = new QueueToken(session.Id, 3, "Skip", null);
        skipped.Call(DateTime.UtcNow);
        await new SkipTokenService(new StubSessionRepository(session), new StubQueueTokenRepository(skipped), publisher).SkipAsync(session.Id, skipped.Id);

        var completed = new QueueToken(session.Id, 4, "Complete", null);
        completed.Call(DateTime.UtcNow);
        await new CompleteTokenService(new StubSessionRepository(session), new StubQueueTokenRepository(completed), publisher).CompleteAsync(session.Id, completed.Id);

        var cancelled = new QueueToken(session.Id, 5, "Cancel", null);
        await new CancelTokenService(new StubSessionRepository(session), new StubQueueTokenRepository(cancelled), publisher).CancelAsync(session.Id, cancelled.Id);

        var late = new QueueToken(session.Id, 6, "Late", null);
        late.Call(DateTime.UtcNow); late.Skip();
        await new MarkTokenLateService(new StubSessionRepository(session), new StubQueueTokenRepository(late), publisher).MarkLateAsync(session.Id, late.Id);

        Assert.Equal(
            new[] {
                QueueEventType.TokenCalled, QueueEventType.TokenRecalled,
                QueueEventType.TokenSkipped, QueueEventType.TokenCompleted,
                QueueEventType.TokenCancelled, QueueEventType.TokenMarkedLate
            },
            publisher.Events.Select(queueEvent => queueEvent.Type));
        Assert.All(publisher.Events, queueEvent => Assert.Equal(session.Id, queueEvent.SessionId));
    }

    [Fact]
    public async Task Cancel_WhenPersistenceFails_DoesNotPublishEvent()
    {
        var session = CreateScheduledSession();
        var token = new QueueToken(session.Id, 1, "Jane Doe", null);
        var publisher = new RecordingPublisher();
        var service = new CancelTokenService(
            new StubSessionRepository(session),
            new StubQueueTokenRepository(token, throwOnSave: true),
            publisher);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.CancelAsync(session.Id, token.Id));

        Assert.Empty(publisher.Events);
    }

    [Fact]
    public async Task PauseAndResume_PublishSessionEventsAfterSave()
    {
        var session = CreateScheduledSession();
        session.OpenBooking();
        session.Start();
        var repository = new StubSessionRepository(session);
        var publisher = new RecordingPublisher(() => repository.WasSaved);

        await new PauseSessionService(repository, publisher)
            .PauseAsync(session.Id);
        repository.WasSaved = false;
        await new ResumeSessionService(repository, publisher)
            .ResumeAsync(session.Id);

        Assert.Equal(
            new[] { QueueEventType.SessionPaused, QueueEventType.SessionResumed },
            publisher.Events.Select(queueEvent => queueEvent.Type));
        Assert.Equal(SessionStatus.Running, publisher.Events[1].SessionStatus);
    }

    private static Session CreateScheduledSession()
    {
        var now = DateTime.UtcNow;
        return new Session(
            Guid.NewGuid(), Guid.NewGuid(), "Event test",
            now.AddMinutes(-30), now.AddMinutes(30),
            now.AddHours(2), now.AddHours(1), 10);
    }

    private static Session CreateRunningSession()
    {
        var session = CreateScheduledSession();
        session.OpenBooking();
        session.Start();
        return session;
    }

    private sealed class RecordingPublisher : IQueueEventPublisher
    {
        private readonly Func<bool>? _publicationAllowed;
        public RecordingPublisher(Func<bool>? publicationAllowed = null) =>
            _publicationAllowed = publicationAllowed;
        public List<QueueChangedEvent> Events { get; } = [];

        public Task PublishAsync(QueueChangedEvent queueEvent, CancellationToken cancellationToken = default)
        {
            if (_publicationAllowed is not null)
                Assert.True(_publicationAllowed(), "Event published before persistence completed.");

            Events.Add(queueEvent);
            return Task.CompletedTask;
        }
    }

    private sealed class StubSessionRepository : ISessionRepository
    {
        private readonly Session? _session;
        public StubSessionRepository(Session? session) => _session = session;
        public bool WasSaved { get; set; }
        public Task<Session?> GetByIdAsync(Guid sessionId, CancellationToken cancellationToken = default) =>
            Task.FromResult(_session?.Id == sessionId ? _session : null);
        public Task AddAsync(Session session, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task SaveChangesAsync(CancellationToken cancellationToken = default)
        {
            WasSaved = true;
            return Task.CompletedTask;
        }
    }

    private sealed class StubQueueTokenRepository : IQueueTokenRepository
    {
        private readonly QueueToken? _token;
        private readonly bool _throwOnSave;
        public StubQueueTokenRepository(QueueToken? token = null, bool throwOnSave = false)
        {
            _token = token;
            _throwOnSave = throwOnSave;
        }
        public bool WasSaved { get; private set; }
        public Task<QueueToken?> GetByIdAsync(Guid sessionId, Guid tokenId, CancellationToken cancellationToken = default) =>
            Task.FromResult(_token?.SessionId == sessionId && _token.Id == tokenId ? _token : null);
        public Task AddAsync(QueueToken queueToken, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task SaveChangesAsync(CancellationToken cancellationToken = default)
        {
            if (_throwOnSave) throw new InvalidOperationException("Save failed.");
            WasSaved = true;
            return Task.CompletedTask;
        }
        public Task<IReadOnlyList<QueueToken>> GetBySessionOrderedAsync(Guid sessionId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<QueueToken?> GetNextWaitingForUpdateAsync(Guid sessionId, CancellationToken cancellationToken = default) => Task.FromResult(_token?.SessionId == sessionId ? _token : null);
        public Task<int?> GetMaximumTokenNumberAsync(Guid sessionId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<int?> AllocateNextTokenNumberAsync(Guid sessionId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }

    private sealed class StubTokenNumberService : ITokenNumberService
    {
        private readonly int _tokenNumber;
        public StubTokenNumberService(int tokenNumber) => _tokenNumber = tokenNumber;
        public Task<int> GetNextTokenNumberAsync(Guid sessionId, CancellationToken cancellationToken = default) => Task.FromResult(_tokenNumber);
    }

    private sealed class StubPatientProfileRepository(PatientProfile patient)
        : IPatientProfileRepository
    {
        public Task<PatientProfile?> GetByIdAsync(
            Guid patientProfileId,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<PatientProfile?>(
                patient.Id == patientProfileId ? patient : null);
    }

    private sealed class StubTransactionManager : ITransactionManager
    {
        public StubTransaction Transaction { get; } = new();
        public Task<ITransaction> BeginAsync(CancellationToken cancellationToken = default) => Task.FromResult<ITransaction>(Transaction);
    }

    private sealed class StubTransaction : ITransaction
    {
        public bool Committed { get; private set; }
        public Task CommitAsync(CancellationToken cancellationToken = default)
        {
            Committed = true;
            return Task.CompletedTask;
        }
        public Task RollbackAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}

using DigiLine.Application.Queue;
using DigiLine.Application.Sessions;
using DigiLine.Domain.Common;
using DigiLine.Domain.Entities;

namespace DigiLine.Application.Tests;

public class GetDoctorSessionsServiceTests
{
    private static readonly DateTime Now = new(2026, 8, 19, 6, 0, 0, DateTimeKind.Utc);

    [Fact]
    public async Task GetAsync_ReturnsBackendOwnedQueueSummaryAndOrderedQueue()
    {
        var clinicId = Guid.NewGuid(); var doctorId = Guid.NewGuid(); var sessionId = Guid.NewGuid();
        var record = Session(sessionId, clinicId, doctorId, SessionStatus.Running,
            Token(3, QueueTokenStatus.Late), Token(1, QueueTokenStatus.Called), Token(2, QueueTokenStatus.Waiting));
        var repository = new StubRepository(record);
        var service = new GetDoctorSessionsService(repository, new TestClock(Now));

        var result = await service.GetAsync(sessionId, clinicId, doctorId);

        Assert.NotNull(result);
        Assert.Equal(new[] { 1, 2, 3 }, result.Queue.Select(token => token.TokenNumber));
        Assert.Equal(1, result.Session.QueueSummary.CurrentToken!.TokenNumber);
        Assert.Equal(1, result.Session.QueueSummary.WaitingCount);
        Assert.Equal(1, result.Session.QueueSummary.LateCount);
        Assert.Equal(3, result.Session.QueueSummary.TotalCount);
        Assert.True(result.Session.IsBookingOpen);
        Assert.Equal((clinicId, doctorId), repository.LastScope);
    }

    [Fact]
    public async Task GetActiveOrUpcomingAsync_PrefersPausedOrRunningSession()
    {
        var clinicId = Guid.NewGuid(); var doctorId = Guid.NewGuid();
        var upcoming = Session(Guid.NewGuid(), clinicId, doctorId, SessionStatus.Scheduled);
        var active = Session(Guid.NewGuid(), clinicId, doctorId, SessionStatus.Paused);
        var service = new GetDoctorSessionsService(new StubRepository(upcoming, active), new TestClock(Now));

        var result = await service.GetActiveOrUpcomingAsync(clinicId, doctorId);

        Assert.NotNull(result);
        Assert.Equal(active.Id, result.Session.Id);
    }

    [Fact]
    public async Task GetAsync_WhenRepositoryRejectsForeignScope_ReturnsNull()
    {
        var record = Session(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), SessionStatus.Running);
        var service = new GetDoctorSessionsService(new StubRepository(record), new TestClock(Now));

        var result = await service.GetAsync(record.Id, Guid.NewGuid(), Guid.NewGuid());

        Assert.Null(result);
    }

    private static DoctorSessionRecord Session(Guid id, Guid clinicId, Guid doctorId, SessionStatus status, params QueueTokenListItem[] queue) =>
        new(id, clinicId, "Clinic", doctorId, "Doctor", "Morning", Now.AddHours(-1), Now.AddMinutes(-30), Now.AddHours(2), Now.AddHours(1), 10, status, queue);

    private static QueueTokenListItem Token(int number, QueueTokenStatus status) =>
        new(Guid.NewGuid(), number, null, $"Patient {number}", null, status, Now.AddMinutes(number), status == QueueTokenStatus.Called ? Now.AddMinutes(number + 5) : null);

    private sealed class TestClock(DateTime now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => new(now);
    }

    private sealed class StubRepository(params DoctorSessionRecord[] records) : ISessionRepository
    {
        public (Guid ClinicId, Guid DoctorId) LastScope { get; private set; }
        public Task<IReadOnlyList<DoctorSessionRecord>> ListForDoctorAsync(Guid clinicId, Guid doctorId, CancellationToken cancellationToken = default) { LastScope = (clinicId, doctorId); return Task.FromResult<IReadOnlyList<DoctorSessionRecord>>(records.Where(record => record.ClinicId == clinicId && record.DoctorId == doctorId).ToList()); }
        public Task<DoctorSessionRecord?> GetForDoctorAsync(Guid sessionId, Guid clinicId, Guid doctorId, CancellationToken cancellationToken = default) { LastScope = (clinicId, doctorId); return Task.FromResult(records.SingleOrDefault(record => record.Id == sessionId && record.ClinicId == clinicId && record.DoctorId == doctorId)); }
        public Task AddAsync(Session session, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task SaveChangesAsync(CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<Session?> GetByIdAsync(Guid sessionId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }
}

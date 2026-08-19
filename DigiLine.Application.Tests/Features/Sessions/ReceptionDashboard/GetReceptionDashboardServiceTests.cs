using DigiLine.Application.Queue;
using DigiLine.Application.Sessions;
using DigiLine.Domain.Common;
using DigiLine.Domain.Entities;

namespace DigiLine.Application.Tests;

public class GetReceptionDashboardServiceTests
{
    private static readonly DateTime Now = new(2026, 8, 19, 8, 0, 0, DateTimeKind.Utc);

    [Fact]
    public async Task GetTodayAsync_UsesClinicAndUtcDayAndBuildsQueueSummaries()
    {
        var clinic = Guid.NewGuid(); var session = Record(clinic);
        var repository = new StubRepository(session);
        var service = new GetReceptionDashboardService(repository, new TestClock(Now));

        var result = await service.GetTodayAsync(clinic);

        Assert.Equal("Clinic", result.ClinicName);
        Assert.Single(result.Sessions);
        Assert.Equal(1, result.Sessions[0].Session.QueueSummary.WaitingCount);
        Assert.Equal((clinic, Now.Date, Now.Date.AddDays(1)), repository.Range);
    }

    [Fact]
    public async Task GetSessionAsync_DoesNotExposeAnotherClinicsSession()
    {
        var record = Record(Guid.NewGuid());
        var service = new GetReceptionDashboardService(new StubRepository(record), new TestClock(Now));
        Assert.Null(await service.GetSessionAsync(record.Id, Guid.NewGuid()));
    }

    private static DoctorSessionRecord Record(Guid clinic) => new(Guid.NewGuid(), clinic, "Clinic",
        Guid.NewGuid(), "Dr Rao", "Morning", Now.AddHours(-2), Now.AddHours(-1), Now.AddHours(3),
        Now.AddHours(2), 10, SessionStatus.Running,
        [new QueueTokenListItem(Guid.NewGuid(), 1, null, "Walk-in", null, QueueTokenStatus.Waiting, Now)]);

    private sealed class TestClock(DateTime now) : TimeProvider { public override DateTimeOffset GetUtcNow() => new(now); }
    private sealed class StubRepository(DoctorSessionRecord record) : ISessionRepository
    {
        public (Guid, DateTime, DateTime) Range { get; private set; }
        public Task<IReadOnlyList<DoctorSessionRecord>> ListForClinicAsync(Guid clinicId, DateTime from, DateTime to, CancellationToken cancellationToken = default) { Range = (clinicId, from, to); return Task.FromResult<IReadOnlyList<DoctorSessionRecord>>(record.ClinicId == clinicId ? [record] : []); }
        public Task<DoctorSessionRecord?> GetForClinicAsync(Guid sessionId, Guid clinicId, CancellationToken cancellationToken = default) => Task.FromResult(record.Id == sessionId && record.ClinicId == clinicId ? record : null);
        public Task AddAsync(Session session, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task SaveChangesAsync(CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<Session?> GetByIdAsync(Guid sessionId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }
}

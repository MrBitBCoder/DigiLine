using DigiLine.Application.Sessions;
using DigiLine.Domain.Common;
using DigiLine.Domain.Entities;

namespace DigiLine.Application.Tests;

public class CreateDoctorSessionServiceTests
{
    [Fact]
    public async Task CreateAsync_RejectsAmbiguousScheduleTimestamp()
    {
        var repository = new StubRepository();
        var start = DateTime.SpecifyKind(DateTime.UtcNow.AddHours(2), DateTimeKind.Unspecified);
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            new CreateDoctorSessionService(repository).CreateAsync(Guid.NewGuid(), Guid.NewGuid(),
                new CreateDoctorSessionRequest("Clinic", start, start.AddHours(1), 10)));
        Assert.Null(repository.Added);
    }

    [Fact]
    public async Task CreateAsync_UsesAuthenticatedScopeAndOpensBooking()
    {
        var repository = new StubRepository();
        var clinicId = Guid.NewGuid();
        var doctorId = Guid.NewGuid();
        var start = DateTime.UtcNow.AddHours(2);

        var id = await new CreateDoctorSessionService(repository).CreateAsync(
            clinicId, doctorId,
            new CreateDoctorSessionRequest(" Morning clinic ", start, start.AddHours(3), 15));

        Assert.Equal(id, repository.Added!.Id);
        Assert.Equal(clinicId, repository.Added.ClinicId);
        Assert.Equal(doctorId, repository.Added.DoctorId);
        Assert.Equal("Morning clinic", repository.Added.Name);
        Assert.Equal(SessionStatus.BookingOpen, repository.Added.Status);
        Assert.Equal(1, repository.SaveCount);
    }

    [Fact]
    public async Task CreateAsync_ScheduledBookingTimeIsPersistedAndNotBookableEarly()
    {
        var repository = new StubRepository();
        var opening = DateTime.UtcNow.AddHours(1);
        var start = opening.AddHours(1);

        await new CreateDoctorSessionService(repository).CreateAsync(Guid.NewGuid(), Guid.NewGuid(),
            new CreateDoctorSessionRequest("Later clinic", start, start.AddHours(2), 10, opening));

        Assert.Equal(opening, repository.Added!.BookingOpenTime);
        Assert.False(repository.Added.IsBookingOpen(opening.AddTicks(-1)));
        Assert.True(repository.Added.IsBookingOpen(opening));
    }

    [Fact]
    public async Task CreateAsync_RejectsBookingOpeningAtSessionStart()
    {
        var repository = new StubRepository();
        var start = DateTime.UtcNow.AddHours(2);
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            new CreateDoctorSessionService(repository).CreateAsync(Guid.NewGuid(), Guid.NewGuid(),
                new CreateDoctorSessionRequest("Clinic", start, start.AddHours(2), 10, start)));
        Assert.Null(repository.Added);
    }

    [Fact]
    public async Task CreateAsync_RejectsPastStartWithoutSaving()
    {
        var repository = new StubRepository();
        var start = DateTime.UtcNow.AddMinutes(-1);
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            new CreateDoctorSessionService(repository).CreateAsync(Guid.NewGuid(), Guid.NewGuid(),
                new CreateDoctorSessionRequest("Clinic", start, start.AddHours(2), 10)));
        Assert.Null(repository.Added);
        Assert.Equal(0, repository.SaveCount);
    }

    private sealed class StubRepository : ISessionRepository
    {
        public Session? Added { get; private set; }
        public int SaveCount { get; private set; }
        public Task AddAsync(Session session, CancellationToken cancellationToken = default)
        { Added = session; return Task.CompletedTask; }
        public Task SaveChangesAsync(CancellationToken cancellationToken = default)
        { SaveCount++; return Task.CompletedTask; }
        public Task<Session?> GetByIdAsync(Guid sessionId, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
    }
}

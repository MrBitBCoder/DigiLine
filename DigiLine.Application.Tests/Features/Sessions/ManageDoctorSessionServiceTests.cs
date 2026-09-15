using DigiLine.Application.Sessions;
using DigiLine.Domain.Entities;

namespace DigiLine.Application.Tests;

public sealed class ManageDoctorSessionServiceTests
{
    [Fact]
    public async Task Update_ChangesOnlyOwnedUnbookedSession()
    {
        var clinic = Guid.NewGuid(); var doctor = Guid.NewGuid();
        var session = NewSession(clinic, doctor);
        var repository = new StubRepository(session);
        var start = DateTime.UtcNow.AddHours(5);

        await new ManageDoctorSessionService(repository).UpdateAsync(clinic, doctor, session.Id,
            new CreateDoctorSessionRequest("Updated", start, start.AddHours(2), 20));

        Assert.Equal("Updated", session.Name);
        Assert.Equal(start, session.StartTime);
        Assert.Equal(1, repository.SaveCount);
    }

    [Fact]
    public async Task Update_ReschedulesBookingOpening()
    {
        var clinic = Guid.NewGuid(); var doctor = Guid.NewGuid();
        var session = NewSession(clinic, doctor);
        var repository = new StubRepository(session);
        var opening = DateTime.UtcNow.AddHours(3);
        var start = opening.AddHours(1);

        await new ManageDoctorSessionService(repository).UpdateAsync(clinic, doctor, session.Id,
            new CreateDoctorSessionRequest("Updated", start, start.AddHours(1), 20, opening));

        Assert.Equal(opening, session.BookingOpenTime);
        Assert.False(session.IsBookingOpen(opening.AddTicks(-1)));
        Assert.True(session.IsBookingOpen(opening));
    }

    [Fact]
    public async Task Delete_RejectsSessionWithToken()
    {
        var clinic = Guid.NewGuid(); var doctor = Guid.NewGuid();
        var session = NewSession(clinic, doctor);
        session.QueueTokens.Add(new QueueToken(session.Id, 1, "Patient", null));
        var repository = new StubRepository(session);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            new ManageDoctorSessionService(repository).DeleteAsync(clinic, doctor, session.Id));

        Assert.False(repository.Deleted);
    }

    [Fact]
    public async Task Delete_DoesNotExposeAnotherDoctorsSession()
    {
        var session = NewSession(Guid.NewGuid(), Guid.NewGuid());
        var repository = new StubRepository(session);

        await Assert.ThrowsAsync<KeyNotFoundException>(() =>
            new ManageDoctorSessionService(repository).DeleteAsync(session.ClinicId, Guid.NewGuid(), session.Id));

        Assert.False(repository.Deleted);
    }

    private static Session NewSession(Guid clinic, Guid doctor)
    {
        var start = DateTime.UtcNow.AddHours(2);
        var session = new Session(clinic, doctor, "Original", DateTime.UtcNow, start, start.AddHours(1), start.AddHours(1), 10);
        session.OpenBooking();
        return session;
    }

    private sealed class StubRepository(Session session) : ISessionRepository
    {
        public int SaveCount { get; private set; }
        public bool Deleted { get; private set; }
        public Task<Session?> GetForDoctorForUpdateAsync(Guid id, Guid clinic, Guid doctor, CancellationToken ct = default) =>
            Task.FromResult<Session?>(session.Id == id && session.ClinicId == clinic && session.DoctorId == doctor ? session : null);
        public Task DeleteAsync(Session value, CancellationToken ct = default) { Deleted = true; return Task.CompletedTask; }
        public Task AddAsync(Session value, CancellationToken ct = default) => Task.CompletedTask;
        public Task SaveChangesAsync(CancellationToken ct = default) { SaveCount++; return Task.CompletedTask; }
        public Task<Session?> GetByIdAsync(Guid id, CancellationToken ct = default) => Task.FromResult<Session?>(null);
    }
}

using DigiLine.Application.Sessions;
using DigiLine.Domain.Entities;
using DigiLine.Domain.Common;

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

    [Theory]
    [InlineData(SessionStatus.BookingOpen)]
    [InlineData(SessionStatus.Running)]
    [InlineData(SessionStatus.Paused)]
    public async Task ZeroTokens_AllowsUpdateAndDeleteIncludingPastStart(SessionStatus status)
    {
        var start = DateTime.UtcNow.AddHours(-1);
        var session = new Session(Guid.NewGuid(), Guid.NewGuid(), "Original", start.AddHours(-1), start, start.AddHours(3), start.AddHours(3), 10);
        session.OpenBooking();
        if (status is SessionStatus.Running or SessionStatus.Paused) session.Start();
        if (status == SessionStatus.Paused) session.Pause();
        var opening = session.BookingOpenTime;
        var repository = new StubRepository(session);
        var service = new ManageDoctorSessionService(repository);
        await service.UpdateAsync(session.ClinicId, session.DoctorId, session.Id,
            new CreateDoctorSessionRequest("Updated", start, session.EndTime, 20));
        Assert.Equal(status, session.Status);
        Assert.Equal(opening, session.BookingOpenTime);
        Assert.Equal("Updated", session.Name);
        await service.DeleteAsync(session.ClinicId, session.DoctorId, session.Id);
        Assert.True(repository.Deleted);
        Assert.Equal(2, repository.SaveCount);
    }

    [Fact]
    public async Task Delete_AllowsFutureSessionWithoutTokens()
    {
        var session = NewSession(Guid.NewGuid(), Guid.NewGuid());
        var repository = new StubRepository(session);
        await new ManageDoctorSessionService(repository).DeleteAsync(session.ClinicId, session.DoctorId, session.Id);
        Assert.True(repository.Deleted);
        Assert.Equal(1, repository.SaveCount);
    }

    [Fact]
    public async Task Update_RejectsSessionWithTokenWithoutSaving()
    {
        var session = NewSession(Guid.NewGuid(), Guid.NewGuid());
        session.QueueTokens.Add(new QueueToken(session.Id, 1, "Patient", null));
        var repository = new StubRepository(session);
        await Assert.ThrowsAsync<InvalidOperationException>(() => new ManageDoctorSessionService(repository).UpdateAsync(
            session.ClinicId, session.DoctorId, session.Id, new CreateDoctorSessionRequest("Updated", session.StartTime, session.EndTime, 20)));
        Assert.Equal(0, repository.SaveCount);
        Assert.Equal("Original", session.Name);
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

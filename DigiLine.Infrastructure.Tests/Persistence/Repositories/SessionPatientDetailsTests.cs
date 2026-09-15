using DigiLine.Domain.Common;
using DigiLine.Domain.Entities;
using DigiLine.Infrastructure.Persistence;
using DigiLine.Infrastructure.Persistence.Repositories;
using Microsoft.EntityFrameworkCore;

namespace DigiLine.Infrastructure.Tests;

public sealed class SessionPatientDetailsTests
{
    [Fact]
    public async Task DoctorAndReceptionSessionQueries_IncludeBookedPatientDetails()
    {
        await using var db = new DigiLineDbContext(new DbContextOptionsBuilder<DigiLineDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        var clinic = new Clinic("Clinic", "clinic");
        var doctor = new Staff(clinic.Id, "Doctor", "doctor@example.test", "hash", StaffRole.Doctor);
        var phone = new PhoneAccount("9999999999");
        var profile = new PatientProfile(phone.Id, "Asha Rao", 34);
        var start = DateTime.UtcNow.AddHours(1);
        var session = new Session(clinic.Id, doctor.Id, "Morning", start.AddHours(-2),
            start, start.AddHours(2), start.AddHours(2), 10);
        db.AddRange(clinic, doctor, phone, profile, session);
        db.QueueTokens.Add(new QueueToken(session.Id, 1, profile.Id));
        var late = new QueueToken(session.Id, 2, "Walk-in", "8888888888", 27);
        late.Call(DateTime.UtcNow.AddMinutes(-10));
        late.Skip();
        late.MarkLate(DateTime.UtcNow);
        db.QueueTokens.Add(late);
        await db.SaveChangesAsync();

        var repository = new SessionRepository(db);
        var record = await repository.GetForDoctorAsync(session.Id, clinic.Id, doctor.Id);

        Assert.NotNull(record);
        Assert.Equal("Asha Rao", record.Queue[0].PatientName);
        Assert.Equal(34, record.Queue[0].PatientAge);
        Assert.Equal("9999999999", record.Queue[0].PhoneNumber);
        Assert.Equal("Walk-in", record.Queue[1].PatientName);
        Assert.Equal(27, record.Queue[1].PatientAge);
        Assert.Equal("8888888888", record.Queue[1].PhoneNumber);
        Assert.Equal(late.LateArrivedAt, record.Queue[1].LateArrivedAt);
    }
}

using DigiLine.Domain.Entities;
using DigiLine.Infrastructure.Persistence;
using DigiLine.Infrastructure.Persistence.Repositories;
using Microsoft.EntityFrameworkCore;

namespace DigiLine.Infrastructure.Tests;

public sealed class QueueTokenRecoveryRepositoryTests
{
    [Theory]
    [InlineData(DigiLine.Domain.Common.QueueTokenStatus.Waiting)]
    [InlineData(DigiLine.Domain.Common.QueueTokenStatus.Called)]
    [InlineData(DigiLine.Domain.Common.QueueTokenStatus.InConsultation)]
    [InlineData(DigiLine.Domain.Common.QueueTokenStatus.Skipped)]
    [InlineData(DigiLine.Domain.Common.QueueTokenStatus.Late)]
    public async Task Recovery_IncludesEveryStillRelevantStatus(DigiLine.Domain.Common.QueueTokenStatus status)
    {
        await using var db = new DigiLineDbContext(new DbContextOptionsBuilder<DigiLineDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        var session = Guid.NewGuid(); var profile = Guid.NewGuid(); var token = new QueueToken(session, 15, profile);
        db.QueueTokens.Add(token);
        db.Entry(token).Property(value => value.Status).CurrentValue = status;
        await db.SaveChangesAsync();

        var recovered = Assert.Single(await new QueueTokenRepository(db).GetRecoverableByPatientAsync(session, profile));
        Assert.Equal(status, recovered.Status);
    }

    [Fact]
    public async Task Recovery_IsScopedAndExcludesFinishedTokens()
    {
        await using var db = new DigiLineDbContext(new DbContextOptionsBuilder<DigiLineDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        var session = Guid.NewGuid(); var profile = Guid.NewGuid();
        var waiting = new QueueToken(session, 15, profile);
        var completed = new QueueToken(session, 16, profile); completed.Call(DateTime.UtcNow); completed.Complete(DateTime.UtcNow);
        var cancelled = new QueueToken(session, 17, profile); cancelled.Cancel();
        var noShow = new QueueToken(session, 18, profile); noShow.Call(DateTime.UtcNow); noShow.Skip(); noShow.FinalizeNoShow(DateTime.UtcNow);
        db.QueueTokens.AddRange(waiting, completed, cancelled, noShow,
            new QueueToken(Guid.NewGuid(), 15, profile), new QueueToken(session, 19, Guid.NewGuid()));
        await db.SaveChangesAsync();

        var result = await new QueueTokenRepository(db).GetRecoverableByPatientAsync(session, profile);

        var recovered = Assert.Single(result);
        Assert.Equal(waiting.Id, recovered.Id);
        Assert.Equal(15, recovered.TokenNumber);
        Assert.Equal(DigiLine.Domain.Common.QueueTokenStatus.Waiting, recovered.Status);
    }
}

using DigiLine.Domain.Entities;
using DigiLine.Infrastructure.Persistence;
using DigiLine.Infrastructure.Persistence.Repositories;
using Microsoft.EntityFrameworkCore;

namespace DigiLine.Infrastructure.Tests;

public class QueueOrderingRepositoryTests
{
    [Fact]
    public async Task GetByIdAsync_ScopesTokenToSession()
    {
        var options = new DbContextOptionsBuilder<DigiLineDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        await using var dbContext = new DigiLineDbContext(options);
        var sessionId = Guid.NewGuid();
        var token = new QueueToken(sessionId, 1, "Jane Doe", null);
        await dbContext.QueueTokens.AddAsync(token);
        await dbContext.SaveChangesAsync();
        var repository = new QueueTokenRepository(dbContext);

        var found = await repository.GetByIdAsync(sessionId, token.Id);
        var wrongSession = await repository.GetByIdAsync(
            Guid.NewGuid(),
            token.Id);

        Assert.Same(token, found);
        Assert.Null(wrongSession);
    }

    [Fact]
    public async Task GetNextWaitingForUpdateAsync_WithoutTransaction_Throws()
    {
        var options = new DbContextOptionsBuilder<DigiLineDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        await using var dbContext = new DigiLineDbContext(options);
        var repository = new QueueTokenRepository(dbContext);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            repository.GetNextWaitingForUpdateAsync(Guid.NewGuid()));

        Assert.Equal(
            "Selecting the next waiting token requires an active transaction.",
            exception.Message);
    }

    [Fact]
    public async Task GetBySessionOrderedAsync_FiltersSessionAndOrdersByTokenNumber()
    {
        var options = new DbContextOptionsBuilder<DigiLineDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        await using var dbContext = new DigiLineDbContext(options);
        var sessionId = Guid.NewGuid();
        var otherSessionId = Guid.NewGuid();

        await dbContext.QueueTokens.AddRangeAsync(
            new QueueToken(sessionId, 3, "Third", null),
            new QueueToken(otherSessionId, 1, "Other session", null),
            new QueueToken(sessionId, 1, "First", null),
            new QueueToken(sessionId, 2, "Second", null));
        await dbContext.SaveChangesAsync();
        var repository = new QueueTokenRepository(dbContext);

        var result = await repository.GetBySessionOrderedAsync(sessionId);

        Assert.Equal(new[] { 1, 2, 3 }, result.Select(token => token.TokenNumber));
        Assert.All(result, token => Assert.Equal(sessionId, token.SessionId));
    }
}

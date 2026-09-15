using DigiLine.Infrastructure.Persistence;
using DigiLine.Infrastructure.Persistence.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace DigiLine.Infrastructure.Tests
{

    public class QueueTokenRepositoryTests
    {

        private static DigiLineDbContext CreateDbContext()
        {
            var configuration = new ConfigurationBuilder()
                .SetBasePath(AppContext.BaseDirectory)
                .AddJsonFile("appsettings.json", optional: false)
                .Build();

            var connectionString =
                Environment.GetEnvironmentVariable("DIGILINE_TEST_CONNECTION") ?? configuration.GetConnectionString("DigiLine");

            if (string.IsNullOrWhiteSpace(connectionString))
            {
                throw new InvalidOperationException(
                    "The 'DigiLine' connection string was not found.");
            }

            var options = new DbContextOptionsBuilder<DigiLineDbContext>()
                .UseSqlServer(connectionString)
                .Options;

            return new DigiLineDbContext(options);
        }

        [Fact]
        public async Task AllocateNextTokenNumberAsync_ShouldReturnCurrentNumber_AndIncrementCounter()
        {
            await using var dbContext = CreateDbContext();

            var session = await dbContext.Sessions
                .OrderBy(x => x.CreatedAt)
                .FirstOrDefaultAsync();

            Assert.NotNull(session);

            var originalNextTokenNumber = session.NextTokenNumber;

            try
            {
                var repository = new QueueTokenRepository(dbContext);

                var allocatedNumber =
                    await repository.AllocateNextTokenNumberAsync(session.Id);

                Assert.Equal(originalNextTokenNumber, allocatedNumber);

                var updatedNextTokenNumber = await dbContext.Sessions
                    .Where(x => x.Id == session.Id)
                    .Select(x => x.NextTokenNumber)
                    .SingleAsync();

                Assert.Equal(originalNextTokenNumber + 1, updatedNextTokenNumber);
            }
            finally
            {
                await dbContext.Database.ExecuteSqlInterpolatedAsync($"""
            UPDATE Sessions
            SET NextTokenNumber = {originalNextTokenNumber}
            WHERE Id = {session.Id};
            """);
            }
        }


        [Fact]
        public async Task AllocateNextTokenNumberAsync_ShouldReturnUniqueConsecutiveNumbers_WhenCalledConcurrently()
        {
            await using var setupContext = CreateDbContext();

            var session = await setupContext.Sessions
                .OrderBy(x => x.CreatedAt)
                .FirstOrDefaultAsync();

            Assert.NotNull(session);

            var sessionId = session.Id;
            var startingNextTokenNumber = session.NextTokenNumber;

            await using var transaction =
                await setupContext.Database.BeginTransactionAsync();

            try
            {
                const int allocationCount = 10;

                var tasks = Enumerable.Range(0, allocationCount)
                    .Select(async _ =>
                    {
                        await using var context = CreateDbContext();

                        var repository = new QueueTokenRepository(context);

                        return await repository.AllocateNextTokenNumberAsync(
                            sessionId);
                    })
                    .ToList();

                var results = await Task.WhenAll(tasks);

                // Every allocation must return a number.
                Assert.All(results, result => Assert.NotNull(result));

                var tokenNumbers = results
                    .Select(result => result!.Value)
                    .OrderBy(number => number)
                    .ToList();

                // Expected sequence:
                // starting value, starting value + 1, ..., starting value + 9
                var expectedNumbers = Enumerable
                    .Range(startingNextTokenNumber, allocationCount)
                    .ToList();

                Assert.Equal(expectedNumbers, tokenNumbers);

                // No duplicate numbers.
                Assert.Equal(
                    allocationCount,
                    tokenNumbers.Distinct().Count());

                // Verify the database counter advanced correctly.
                await setupContext.Entry(session).ReloadAsync();

                Assert.Equal(
                    startingNextTokenNumber + allocationCount,
                    session.NextTokenNumber);
            }
            finally
            {
                // Roll back every database change made by this test.
                await transaction.RollbackAsync();
            }
        }
    }
}

using DigiLine.Infrastructure.Persistence;
using DigiLine.Infrastructure.Persistence.Transactions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DigiLine.Infrastructure.Tests
{

    public class TransactionManagerTests
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
        public async Task RollbackAsync_ShouldUndoChangesMadeInsideTransaction()
        {
            await using var dbContext = CreateDbContext();

            var existingSession = await dbContext.Sessions
                .OrderBy(x => x.CreatedAt)
                .FirstOrDefaultAsync();

            Assert.NotNull(existingSession);

            var now = DateTime.UtcNow;
            var session = new DigiLine.Domain.Entities.Session(
                existingSession.ClinicId,
                existingSession.DoctorId,
                "Rollback Test",
                now.AddMinutes(-30),
                now.AddMinutes(30),
                now.AddHours(2),
                now.AddHours(1),
                10);

            await dbContext.Sessions.AddAsync(session);
            await dbContext.SaveChangesAsync();

            var originalNextTokenNumber = session.NextTokenNumber;

            var transactionManager =
                new EfTransactionManager(dbContext);

            await using var transaction =
                await transactionManager.BeginAsync();

            var repository =
                new DigiLine.Infrastructure.Persistence.Repositories.QueueTokenRepository(
                    dbContext);

            var allocatedNumber =
                await repository.AllocateNextTokenNumberAsync(session.Id);

            Assert.Equal(
                originalNextTokenNumber,
                allocatedNumber);

            await transaction.RollbackAsync();

            var nextTokenNumberAfterRollback =
                await dbContext.Sessions
                    .Where(x => x.Id == session.Id)
                    .Select(x => x.NextTokenNumber)
                    .SingleAsync();

            Assert.Equal(
                originalNextTokenNumber,
                nextTokenNumberAfterRollback);

            dbContext.Sessions.Remove(session);
            await dbContext.SaveChangesAsync();
        }
    }
}

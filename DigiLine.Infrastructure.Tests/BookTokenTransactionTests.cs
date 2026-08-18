using DigiLine.Application.Queue;
using DigiLine.Infrastructure.Persistence;
using DigiLine.Infrastructure.Persistence.Repositories;
using DigiLine.Infrastructure.Persistence.Transactions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace DigiLine.Infrastructure.Tests
{
    public class BookTokenTransactionTests
    {
        private static DigiLineDbContext CreateDbContext()
        {
            var configuration = new ConfigurationBuilder()
                .SetBasePath(AppContext.BaseDirectory)
                .AddJsonFile("appsettings.json", optional: false)
                .Build();

            var connectionString =
                configuration.GetConnectionString("DigiLine");

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
        public async Task BookAsync_WhenQueueTokenPersistenceFails_RollsBackTokenAllocation()
        {
            await using var dbContext = CreateDbContext();

            var clinic = await dbContext.Clinics
                .OrderBy(x => x.CreatedAt)
                .FirstOrDefaultAsync();

            var doctor = await dbContext.Staff
                .OrderBy(x => x.CreatedAt)
                .FirstOrDefaultAsync();

            var patient = await dbContext.PatientProfiles
                .OrderBy(x => x.CreatedAt)
                .FirstOrDefaultAsync();

            Assert.NotNull(clinic);
            Assert.NotNull(doctor);
            Assert.NotNull(patient);

            var now = DateTime.UtcNow;

            var session = new DigiLine.Domain.Entities.Session(
                clinic.Id,
                doctor.Id,
                "Booking Transaction Failure Test",
                now.AddMinutes(-30),
                now.AddMinutes(30),
                now.AddHours(2),
                now.AddHours(1),
                10);

            await dbContext.Sessions.AddAsync(session);
            await dbContext.SaveChangesAsync();

            var conflictingToken = new DigiLine.Domain.Entities.QueueToken(
                session.Id,
                session.NextTokenNumber,
                patient.Id);

            await dbContext.QueueTokens.AddAsync(conflictingToken);
            await dbContext.SaveChangesAsync();

            var originalNextTokenNumber = session.NextTokenNumber;

            var queueTokenRepository =
                new QueueTokenRepository(dbContext);

            var tokenNumberService =
                new TokenNumberService(queueTokenRepository);

            var sessionRepository =
                new SessionRepository(dbContext);

            var patientProfileRepository =
                new PatientProfileRepository(dbContext);

            var transactionManager =
                new EfTransactionManager(dbContext);

            var bookTokenService = new BookTokenService(
                tokenNumberService,
                queueTokenRepository,
                sessionRepository,
                patientProfileRepository,
                transactionManager);

            await Assert.ThrowsAsync<DbUpdateException>(
                () => bookTokenService.BookAsync(
                    session.Id,
                    patient.Id));

            dbContext.ChangeTracker.Clear();

            var sessionAfterFailure = await dbContext.Sessions
                .Where(x => x.Id == session.Id)
                .SingleAsync();

            var tokensAfterFailure = await dbContext.QueueTokens
                .Where(x => x.SessionId == session.Id)
                .OrderBy(x => x.TokenNumber)
                .ToListAsync();

            Assert.Equal(
                originalNextTokenNumber,
                sessionAfterFailure.NextTokenNumber);

            Assert.Single(tokensAfterFailure);

            Assert.Equal(
                originalNextTokenNumber,
                tokensAfterFailure[0].TokenNumber);

            dbContext.QueueTokens.Remove(tokensAfterFailure[0]);
            dbContext.Sessions.Remove(sessionAfterFailure);
            await dbContext.SaveChangesAsync();
        }
    }
}

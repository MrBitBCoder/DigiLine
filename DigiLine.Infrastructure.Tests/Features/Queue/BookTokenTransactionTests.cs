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

        [Fact]
        public async Task BookAsync_WhenSessionDoesNotExist_ThrowsSessionNotFound()
        {
            await using var dbContext = CreateDbContext();

            var patient = await dbContext.PatientProfiles
                .OrderBy(x => x.CreatedAt)
                .FirstOrDefaultAsync();

            Assert.NotNull(patient);

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

            var invalidSessionId = Guid.NewGuid();

            var exception = await Assert.ThrowsAsync<InvalidOperationException>(
                () => bookTokenService.BookAsync(
                    invalidSessionId,
                    patient.Id));

            Assert.Equal(
                "Session was not found.",
                exception.Message);

            var tokenCount = await dbContext.QueueTokens
                .CountAsync(x => x.SessionId == invalidSessionId);

            Assert.Equal(0, tokenCount);
        }

        [Fact]
        public async Task BookAsync_WhenSessionIsClosed_ThrowsClosedSessionException()
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
                "Closed Session Booking Test",
                now.AddMinutes(-30),
                now.AddMinutes(-10),
                now.AddHours(1),
                now.AddMinutes(30),
                10);

            await dbContext.Sessions.AddAsync(session);
            await dbContext.SaveChangesAsync();

            session.OpenBooking();
            session.Start();
            session.Close();

            await dbContext.SaveChangesAsync();

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

            var exception = await Assert.ThrowsAsync<InvalidOperationException>(
                () => bookTokenService.BookAsync(
                    session.Id,
                    patient.Id));

            Assert.Equal(
                "Cannot book a token for a closed session.",
                exception.Message);

            var tokenCount = await dbContext.QueueTokens
                .CountAsync(x => x.SessionId == session.Id);

            Assert.Equal(0, tokenCount);

            dbContext.Sessions.Remove(session);
            await dbContext.SaveChangesAsync();
        }

        [Fact]
        public async Task BookAsync_WhenBookingWindowIsClosed_ThrowsBookingClosedException()
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
                "Booking Window Closed Test",
                now.AddHours(-2),
                now.AddHours(-1),
                now.AddHours(2),
                now.AddMinutes(-30),
                10);

            await dbContext.Sessions.AddAsync(session);
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

            var exception = await Assert.ThrowsAsync<InvalidOperationException>(
                () => bookTokenService.BookAsync(
                    session.Id,
                    patient.Id));

            Assert.Equal(
                "Booking is not currently open for this session.",
                exception.Message);

            var tokenCount = await dbContext.QueueTokens
                .CountAsync(x => x.SessionId == session.Id);

            Assert.Equal(0, tokenCount);

            var nextTokenNumber = await dbContext.Sessions
                .Where(x => x.Id == session.Id)
                .Select(x => x.NextTokenNumber)
                .SingleAsync();

            Assert.Equal(
                originalNextTokenNumber,
                nextTokenNumber);

            dbContext.Sessions.Remove(session);
            await dbContext.SaveChangesAsync();
        }

        [Fact]
        public async Task BookAsync_WhenPatientProfileDoesNotExist_ThrowsPatientNotFound()
        {
            await using var dbContext = CreateDbContext();

            var clinic = await dbContext.Clinics
                .OrderBy(x => x.CreatedAt)
                .FirstOrDefaultAsync();

            var doctor = await dbContext.Staff
                .OrderBy(x => x.CreatedAt)
                .FirstOrDefaultAsync();

            Assert.NotNull(clinic);
            Assert.NotNull(doctor);

            var now = DateTime.UtcNow;

            var session = new DigiLine.Domain.Entities.Session(
                clinic.Id,
                doctor.Id,
                "Missing Patient Test",
                now.AddMinutes(-30),
                now.AddMinutes(30),
                now.AddHours(2),
                now.AddHours(1),
                10);

            await dbContext.Sessions.AddAsync(session);
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

            var missingPatientProfileId = Guid.NewGuid();

            var exception = await Assert.ThrowsAsync<InvalidOperationException>(
                () => bookTokenService.BookAsync(
                    session.Id,
                    missingPatientProfileId));

            Assert.Equal(
                "Patient profile was not found.",
                exception.Message);

            var tokenCount = await dbContext.QueueTokens
                .CountAsync(x => x.SessionId == session.Id);

            Assert.Equal(0, tokenCount);

            var nextTokenNumber = await dbContext.Sessions
                .Where(x => x.Id == session.Id)
                .Select(x => x.NextTokenNumber)
                .SingleAsync();

            Assert.Equal(
                originalNextTokenNumber,
                nextTokenNumber);

            dbContext.Sessions.Remove(session);
            await dbContext.SaveChangesAsync();
        }

        [Fact]
        public async Task BookAsync_WhenMultiplePatientsBookConcurrently_ReturnsUniqueConsecutiveTokenNumbers()
        {
            const int bookingCount = 10;

            await using var setupDbContext = CreateDbContext();

            var clinic = await setupDbContext.Clinics
                .OrderBy(x => x.CreatedAt)
                .FirstOrDefaultAsync();

            var doctor = await setupDbContext.Staff
                .OrderBy(x => x.CreatedAt)
                .FirstOrDefaultAsync();

            Assert.NotNull(clinic);
            Assert.NotNull(doctor);

            var now = DateTime.UtcNow;

            var session = new DigiLine.Domain.Entities.Session(
                clinic.Id,
                doctor.Id,
                "Concurrent Booking Test",
                now.AddMinutes(-30),
                now.AddMinutes(30),
                now.AddHours(2),
                now.AddHours(1),
                10);

            await setupDbContext.Sessions.AddAsync(session);

            var patientProfileIds = new List<Guid>();

            for (var i = 1; i <= bookingCount; i++)
            {
                var phoneAccount =
                    new DigiLine.Domain.Entities.PhoneAccount(
                        $"90000000{i:00}");

                await setupDbContext.PhoneAccounts.AddAsync(phoneAccount);

                var patientProfile =
                    new DigiLine.Domain.Entities.PatientProfile(
                        phoneAccount.Id,
                        $"Concurrent Patient {i}",
                        30);

                await setupDbContext.PatientProfiles.AddAsync(patientProfile);

                patientProfileIds.Add(patientProfile.Id);
            }

            await setupDbContext.SaveChangesAsync();

            var connectionString =
                setupDbContext.Database.GetConnectionString();

            Assert.False(
                string.IsNullOrWhiteSpace(connectionString));

            var bookingTasks = patientProfileIds
                .Select(patientProfileId =>
                    BookTokenUsingSeparateContextAsync(
                        connectionString!,
                        session.Id,
                        patientProfileId))
                .ToList();

            var tokenNumbers = await Task.WhenAll(bookingTasks);

            Assert.Equal(
                bookingCount,
                tokenNumbers.Length);

            Assert.Equal(
                bookingCount,
                tokenNumbers.Distinct().Count());

            var expectedTokenNumbers =
                Enumerable.Range(1, bookingCount)
                    .ToArray();

            var actualTokenNumbers =
                tokenNumbers
                    .OrderBy(x => x)
                    .ToArray();

            Assert.Equal(
                expectedTokenNumbers,
                actualTokenNumbers);

            setupDbContext.ChangeTracker.Clear();

            var persistedTokens = await setupDbContext.QueueTokens
                .Where(x => x.SessionId == session.Id)
                .OrderBy(x => x.TokenNumber)
                .ToListAsync();

            Assert.Equal(
                bookingCount,
                persistedTokens.Count);

            Assert.Equal(
                expectedTokenNumbers,
                persistedTokens
                    .Select(x => x.TokenNumber)
                    .ToArray());

            var persistedSession = await setupDbContext.Sessions
                .Where(x => x.Id == session.Id)
                .SingleAsync();

            Assert.Equal(
                bookingCount + 1,
                persistedSession.NextTokenNumber);

            setupDbContext.QueueTokens.RemoveRange(
                persistedTokens);

            var createdPatients = await setupDbContext.PatientProfiles
                .Where(x => patientProfileIds.Contains(x.Id))
                .ToListAsync();

            var phoneAccountIds = createdPatients
                .Select(x => x.PhoneAccountId)
                .ToList();

            setupDbContext.PatientProfiles.RemoveRange(
                createdPatients);

            var createdPhoneAccounts = await setupDbContext.PhoneAccounts
                .Where(x => phoneAccountIds.Contains(x.Id))
                .ToListAsync();

            setupDbContext.PhoneAccounts.RemoveRange(
                createdPhoneAccounts);

            setupDbContext.Sessions.Remove(
                persistedSession);

            await setupDbContext.SaveChangesAsync();
        }

        private static async Task<int> BookTokenUsingSeparateContextAsync(
    string connectionString,
    Guid sessionId,
    Guid patientProfileId)
        {
            var options =
                new DbContextOptionsBuilder<DigiLineDbContext>()
                    .UseSqlServer(connectionString)
                    .Options;

            await using var dbContext =
                new DigiLineDbContext(options);

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

            var bookTokenService =
                new BookTokenService(
                    tokenNumberService,
                    queueTokenRepository,
                    sessionRepository,
                    patientProfileRepository,
                    transactionManager);

            return await bookTokenService.BookAsync(
                sessionId,
                patientProfileId);
        }
    }
}

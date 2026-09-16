using DigiLine.Application.Queue;
using DigiLine.Domain.Common;
using DigiLine.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DigiLine.Infrastructure.Persistence.Repositories
{
    public class QueueTokenRepository : IQueueTokenRepository
    {
        private readonly DigiLineDbContext _dbContext;

        public QueueTokenRepository(DigiLineDbContext dbContext)
        {
            _dbContext = dbContext;
        }

        public async Task<IReadOnlyList<QueueToken>> GetBySessionOrderedAsync(
            Guid sessionId,
            CancellationToken cancellationToken = default)
        {
            return await _dbContext.QueueTokens
                .AsNoTracking()
                .Where(token => token.SessionId == sessionId)
                .OrderBy(token => token.QueueOrder)
                .ToListAsync(cancellationToken);
        }

        public async Task<QueueToken?> GetNextWaitingForUpdateAsync(
            Guid sessionId,
            CancellationToken cancellationToken = default)
        {
            if (_dbContext.Database.CurrentTransaction is null)
            {
                throw new InvalidOperationException(
                    "Selecting the next waiting token requires an active transaction.");
            }

            return await _dbContext.QueueTokens
                .FromSqlInterpolated($"""
                    SELECT TOP (1) *
                    FROM QueueTokens WITH (UPDLOCK, READPAST, ROWLOCK)
                    WHERE SessionId = {sessionId}
                      AND Status IN ({QueueTokenStatus.Waiting}, {QueueTokenStatus.Late})
                    ORDER BY
                        QueueOrder,
                        TokenNumber
                    """)
                .SingleOrDefaultAsync(cancellationToken);
        }

        public async Task<QueueToken?> GetCurrentForUpdateAsync(
            Guid sessionId,
            CancellationToken cancellationToken = default)
        {
            if (_dbContext.Database.CurrentTransaction is null)
                throw new InvalidOperationException("Selecting the current token requires an active transaction.");

            return await _dbContext.QueueTokens
                .FromSqlInterpolated($"""
                    SELECT TOP (1) *
                    FROM QueueTokens WITH (UPDLOCK, HOLDLOCK, ROWLOCK)
                    WHERE SessionId = {sessionId}
                      AND Status IN ({QueueTokenStatus.Called}, {QueueTokenStatus.InConsultation})
                    ORDER BY CalledAt DESC, QueueOrder, TokenNumber
                    """)
                .SingleOrDefaultAsync(cancellationToken);
        }

        public async Task<QueueToken?> GetByIdAsync(
            Guid sessionId,
            Guid tokenId,
            CancellationToken cancellationToken = default)
        {
            return await _dbContext.QueueTokens
                .SingleOrDefaultAsync(
                    token => token.SessionId == sessionId
                        && token.Id == tokenId,
                    cancellationToken);
        }

        public Task<QueueToken?> GetByIdForUpdateAsync(
            Guid sessionId,
            Guid tokenId,
            CancellationToken cancellationToken = default)
        {
            if (_dbContext.Database.CurrentTransaction is null)
                throw new InvalidOperationException("Selecting a queue token for update requires an active transaction.");
            return _dbContext.QueueTokens.FromSqlInterpolated($"""
                SELECT * FROM QueueTokens WITH (UPDLOCK, HOLDLOCK)
                WHERE SessionId = {sessionId} AND Id = {tokenId}
                """).SingleOrDefaultAsync(cancellationToken);
        }

        public Task<QueueToken?> GetActiveByPatientAsync(
            Guid sessionId,
            Guid patientProfileId,
            CancellationToken cancellationToken = default) =>
            _dbContext.QueueTokens.AsNoTracking()
                .Where(token => token.SessionId == sessionId
                    && token.PatientProfileId == patientProfileId
                    && token.Status != QueueTokenStatus.Completed
                    && token.Status != QueueTokenStatus.Cancelled
                    && token.Status != QueueTokenStatus.NoShow)
                .OrderByDescending(token => token.CreatedAt)
                .FirstOrDefaultAsync(cancellationToken);

        public async Task<IReadOnlyList<QueueToken>> GetRecoverableByPatientAsync(
            Guid sessionId,
            Guid patientProfileId,
            CancellationToken cancellationToken = default) =>
            await _dbContext.QueueTokens.AsNoTracking()
                .Where(token => token.SessionId == sessionId
                    && token.PatientProfileId == patientProfileId
                    && (token.Status == QueueTokenStatus.Waiting
                        || token.Status == QueueTokenStatus.Called
                        || token.Status == QueueTokenStatus.InConsultation
                        || token.Status == QueueTokenStatus.Skipped
                        || token.Status == QueueTokenStatus.Late))
                .OrderByDescending(token => token.CreatedAt)
                .ThenByDescending(token => token.Id)
                .Take(2)
                .ToListAsync(cancellationToken);

        public async Task<int?> GetMaximumTokenNumberAsync(
            Guid sessionId,
            CancellationToken cancellationToken = default)
        {
            return await _dbContext.QueueTokens
                .Where(x => x.SessionId == sessionId)
                .Select(x => (int?)x.TokenNumber)
                .MaxAsync(cancellationToken);
        }

        public async Task<int?> AllocateNextTokenNumberAsync(
    Guid sessionId,
    CancellationToken cancellationToken = default)
        {
            var connection = _dbContext.Database.GetDbConnection();

            var transaction = _dbContext.Database.CurrentTransaction;

            await using var command = connection.CreateCommand();

            command.CommandText = """
        UPDATE Sessions
        SET NextTokenNumber = NextTokenNumber + 1
        OUTPUT INSERTED.NextTokenNumber - 1
        WHERE Id = @SessionId;
        """;

            var parameter = command.CreateParameter();
            parameter.ParameterName = "@SessionId";
            parameter.Value = sessionId;
            command.Parameters.Add(parameter);

            if (connection.State != System.Data.ConnectionState.Open)
            {
                await connection.OpenAsync(cancellationToken);
            }

            if (transaction is not null)
            {
                command.Transaction = transaction.GetDbTransaction();
            }


            var result = await command.ExecuteScalarAsync(cancellationToken);

            if (result is null || result == DBNull.Value)
            {
                return null;
            }

            return Convert.ToInt32(result);
        }

        public async Task<long?> AllocateNextQueueOrderAsync(
            Guid sessionId,
            CancellationToken cancellationToken = default)
        {
            var connection = _dbContext.Database.GetDbConnection();
            var transaction = _dbContext.Database.CurrentTransaction
                ?? throw new InvalidOperationException("Queue order allocation requires an active transaction.");
            await using var command = connection.CreateCommand();
            command.CommandText = """
                UPDATE Sessions
                SET NextQueueOrder = NextQueueOrder + 1
                OUTPUT INSERTED.NextQueueOrder - 1
                WHERE Id = @SessionId;
                """;
            var parameter = command.CreateParameter();
            parameter.ParameterName = "@SessionId";
            parameter.Value = sessionId;
            command.Parameters.Add(parameter);
            if (connection.State != System.Data.ConnectionState.Open)
                await connection.OpenAsync(cancellationToken);
            command.Transaction = transaction.GetDbTransaction();
            var result = await command.ExecuteScalarAsync(cancellationToken);
            return result is null or DBNull ? null : Convert.ToInt64(result);
        }

        public async Task AddAsync(
                    QueueToken queueToken,
                    CancellationToken cancellationToken = default)
        {
            await _dbContext.QueueTokens.AddAsync(
                queueToken,
                cancellationToken);
        }

        public async Task SaveChangesAsync(
                     CancellationToken cancellationToken = default)
        {
            await _dbContext.SaveChangesAsync(cancellationToken);
        }
    }
}

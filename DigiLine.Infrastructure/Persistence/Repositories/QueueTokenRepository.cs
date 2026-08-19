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
                .OrderBy(token => token.TokenNumber)
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
                        CASE WHEN Status = {QueueTokenStatus.Waiting} THEN 0 ELSE 1 END,
                        TokenNumber
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

using DigiLine.Application.Transactions;
using Microsoft.EntityFrameworkCore.Storage;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DigiLine.Infrastructure.Persistence.Transactions
{
    public class EfTransactionManager : ITransactionManager
    {
        private readonly DigiLineDbContext _dbContext;

        public EfTransactionManager(DigiLineDbContext dbContext)
        {
            _dbContext = dbContext;
        }

        public async Task<ITransaction> BeginAsync(
            CancellationToken cancellationToken = default)
        {
            var transaction =
                await _dbContext.Database.BeginTransactionAsync(
                    cancellationToken);

            return new EfTransaction(transaction);
        }

        private sealed class EfTransaction : ITransaction
        {
            private readonly IDbContextTransaction _transaction;

            public EfTransaction(IDbContextTransaction transaction)
            {
                _transaction = transaction;
            }

            public async Task CommitAsync(
                CancellationToken cancellationToken = default)
            {
                await _transaction.CommitAsync(cancellationToken);
            }

            public async Task RollbackAsync(
                CancellationToken cancellationToken = default)
            {
                await _transaction.RollbackAsync(cancellationToken);
            }

            public async ValueTask DisposeAsync()
            {
                await _transaction.DisposeAsync();
            }
        }
    }
}

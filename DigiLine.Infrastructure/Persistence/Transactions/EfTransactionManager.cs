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

            return new EfTransaction(transaction, _dbContext);
        }

        private sealed class EfTransaction : ITransaction
        {
            private readonly IDbContextTransaction _transaction;
            private readonly DigiLineDbContext _dbContext;
            private bool _disposed;

            public EfTransaction(
                IDbContextTransaction transaction,
                DigiLineDbContext dbContext)
            {
                _transaction = transaction;
                _dbContext = dbContext;
            }

            public async Task CommitAsync(
                CancellationToken cancellationToken = default)
            {
                await _transaction.CommitAsync(cancellationToken);
                await DisposeAsync(); // Release the connection before post-commit event publication opens another scope.
            }

            public async Task RollbackAsync(
                CancellationToken cancellationToken = default)
            {
                await _transaction.RollbackAsync(cancellationToken);
                _dbContext.ChangeTracker.Clear();
                await DisposeAsync();
            }

            public async ValueTask DisposeAsync()
            {
                if (_disposed) return;
                _disposed = true;
                await _transaction.DisposeAsync();
            }
        }
    }
}

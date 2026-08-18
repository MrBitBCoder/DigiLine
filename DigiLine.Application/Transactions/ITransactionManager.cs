using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DigiLine.Application.Transactions
{
    public interface ITransactionManager
    {
        Task<ITransaction> BeginAsync(
            CancellationToken cancellationToken = default);
    }

    public interface ITransaction : IAsyncDisposable
    {
        Task CommitAsync(
            CancellationToken cancellationToken = default);

        Task RollbackAsync(
            CancellationToken cancellationToken = default);
    }
}

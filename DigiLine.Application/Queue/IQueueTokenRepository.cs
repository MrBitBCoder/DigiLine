using DigiLine.Domain.Entities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DigiLine.Application.Queue
{
    public interface IQueueTokenRepository
    {
        Task<int?> GetMaximumTokenNumberAsync(
            Guid sessionId,
            CancellationToken cancellationToken = default);

        Task<int?> AllocateNextTokenNumberAsync(
            Guid sessionId,
            CancellationToken cancellationToken = default);

        Task AddAsync(
            QueueToken queueToken,
            CancellationToken cancellationToken = default);

        Task SaveChangesAsync(
            CancellationToken cancellationToken = default);
    }
}

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
        Task<IReadOnlyList<QueueToken>> GetBySessionOrderedAsync(
            Guid sessionId,
            CancellationToken cancellationToken = default);

        Task<QueueToken?> GetNextWaitingForUpdateAsync(
            Guid sessionId,
            CancellationToken cancellationToken = default);

        Task<QueueToken?> GetByIdAsync(
            Guid sessionId,
            Guid tokenId,
            CancellationToken cancellationToken = default);

        Task<QueueToken?> GetActiveByPatientAsync(
            Guid sessionId,
            Guid patientProfileId,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

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

using DigiLine.Domain.Entities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DigiLine.Application.Sessions
{
    public interface ISessionRepository
    {
        Task AddAsync(
            Session session,
            CancellationToken cancellationToken = default);

        Task SaveChangesAsync(
            CancellationToken cancellationToken = default);

        Task<Session?> GetByIdAsync(
            Guid sessionId,
            CancellationToken cancellationToken = default);
    }
}

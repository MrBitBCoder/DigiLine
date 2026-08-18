using DigiLine.Application.Sessions;
using DigiLine.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DigiLine.Infrastructure.Persistence.Repositories
{

    public class SessionRepository : ISessionRepository
    {
        private readonly DigiLineDbContext _dbContext;

        public SessionRepository(DigiLineDbContext dbContext)
        {
            _dbContext = dbContext;
        }

        public async Task AddAsync(
            Session session,
            CancellationToken cancellationToken = default)
        {
            await _dbContext.Sessions.AddAsync(
                session,
                cancellationToken);
        }

        public async Task SaveChangesAsync(
            CancellationToken cancellationToken = default)
        {
            await _dbContext.SaveChangesAsync(cancellationToken);
        }

        public async Task<Session?> GetByIdAsync(
            Guid sessionId,
            CancellationToken cancellationToken = default)
        {
            return await _dbContext.Sessions
                .FirstOrDefaultAsync(
                    x => x.Id == sessionId,
                    cancellationToken);
        }
    }
}

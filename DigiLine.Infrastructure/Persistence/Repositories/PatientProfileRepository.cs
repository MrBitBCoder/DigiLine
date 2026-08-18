using DigiLine.Application.Patients;
using DigiLine.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DigiLine.Infrastructure.Persistence.Repositories
{
    public class PatientProfileRepository : IPatientProfileRepository
    {
        private readonly DigiLineDbContext _dbContext;

        public PatientProfileRepository(DigiLineDbContext dbContext)
        {
            _dbContext = dbContext;
        }

        public async Task<PatientProfile?> GetByIdAsync(
            Guid patientProfileId,
            CancellationToken cancellationToken = default)
        {
            return await _dbContext.PatientProfiles
                .FirstOrDefaultAsync(
                    x => x.Id == patientProfileId,
                    cancellationToken);
        }
    }
}

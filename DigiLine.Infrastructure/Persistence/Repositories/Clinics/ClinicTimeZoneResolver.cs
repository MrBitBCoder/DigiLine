using DigiLine.Application.Clinics;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace DigiLine.Infrastructure.Persistence.Repositories;

public sealed class ClinicTimeZoneResolver(DigiLineDbContext db, IConfiguration configuration) : IClinicTimeZoneResolver
{
    public async Task<string> GetTimeZoneIdAsync(Guid clinicId, CancellationToken cancellationToken = default) =>
        await db.Clinics.Where(clinic => clinic.Id == clinicId)
            .Select(clinic => clinic.TimeZoneId).SingleOrDefaultAsync(cancellationToken)
        ?? configuration["ClinicTime:DefaultTimeZoneId"] ?? "Asia/Kolkata";
}

using DigiLine.Application.Patients;
using DigiLine.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace DigiLine.Infrastructure.Persistence.Repositories;

public sealed class PublicPatientRepository : IPublicPatientRepository
{
    private readonly DigiLineDbContext _dbContext;
    public PublicPatientRepository(DigiLineDbContext dbContext) => _dbContext = dbContext;

    public async Task<IReadOnlyList<PublicPatientProfile>> ListByPhoneAsync(string phoneNumber, CancellationToken cancellationToken = default) =>
        await _dbContext.PatientProfiles.AsNoTracking()
            .Where(profile => profile.PhoneAccount.PhoneNumber == phoneNumber)
            .OrderBy(profile => profile.Name)
            .Select(profile => new PublicPatientProfile(profile.Id, profile.Name, profile.Age))
            .ToListAsync(cancellationToken);

    public async Task<PublicPatientProfile> CreateAsync(string phoneNumber, string name, int age, CancellationToken cancellationToken = default)
    {
        var account = await _dbContext.PhoneAccounts.SingleOrDefaultAsync(value => value.PhoneNumber == phoneNumber, cancellationToken);
        if (account is null) { account = new PhoneAccount(phoneNumber); _dbContext.PhoneAccounts.Add(account); }
        var profile = new PatientProfile(account.Id, name, age);
        _dbContext.PatientProfiles.Add(profile);
        await _dbContext.SaveChangesAsync(cancellationToken);
        return new PublicPatientProfile(profile.Id, profile.Name, profile.Age);
    }

    public Task<bool> IsOwnedByPhoneAsync(Guid profileId, string phoneNumber, CancellationToken cancellationToken = default) =>
        _dbContext.PatientProfiles.AnyAsync(profile => profile.Id == profileId && profile.PhoneAccount.PhoneNumber == phoneNumber, cancellationToken);
}

using DigiLine.Application.Common.Interfaces;
using DigiLine.Domain.Common;
using DigiLine.Domain.Entities;
using DigiLine.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace DigiLine.API.Development
{

    public static class DevelopmentDataSeeder
    {
        public static async Task SeedAsync(
            IServiceProvider services)
        {
            using var scope = services.CreateScope();

            var dbContext = scope.ServiceProvider
                .GetRequiredService<DigiLineDbContext>();

            var passwordHasher = scope.ServiceProvider
                .GetRequiredService<IPasswordHasher>();

            const string clinicSlug = "dev-clinic";
            const string email = "doctor@dev.local";
            const string password = "DevPassword123!";

            const string phoneNumber = "9876543210";
            const string patientName = "Development Patient";
            const int patientAge = 30;


            var clinic = await dbContext.Clinics
                .FirstOrDefaultAsync(x => x.Slug == clinicSlug);

            if (clinic is null)
            {
                clinic = new Clinic(
                    "Development Clinic",
                    clinicSlug);

                dbContext.Clinics.Add(clinic);
                await dbContext.SaveChangesAsync();
            }

            var staffExists = await dbContext.Staff
                .AnyAsync(x =>
                    x.ClinicId == clinic.Id &&
                    x.Email == email);

            if (!staffExists)
            {
                var passwordHash = passwordHasher.Hash(password);

                var staff = new Staff(
                    clinic.Id,
                    "Development Doctor",
                    email,
                    passwordHash,
                    StaffRole.Doctor);

                dbContext.Staff.Add(staff);

                await dbContext.SaveChangesAsync();
            }

            var phoneAccount = await dbContext.PhoneAccounts.FirstOrDefaultAsync(x => x.PhoneNumber == phoneNumber); if (phoneAccount is null) { phoneAccount = new PhoneAccount(phoneNumber); dbContext.PhoneAccounts.Add(phoneAccount); await dbContext.SaveChangesAsync(); }
            var patientProfileExists = await dbContext.PatientProfiles.AnyAsync(x => x.PhoneAccountId == phoneAccount.Id && x.Name == patientName); if (!patientProfileExists) { var patientProfile = new PatientProfile(phoneAccount.Id, patientName, patientAge); dbContext.PatientProfiles.Add(patientProfile); await dbContext.SaveChangesAsync(); }

        }
    }
}

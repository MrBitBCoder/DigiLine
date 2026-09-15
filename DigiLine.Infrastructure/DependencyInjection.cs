using DigiLine.Application.Common.Authentication;
using DigiLine.Application.Common.Interfaces;
using DigiLine.Application.Clinics;
using DigiLine.Application.Patients;
using DigiLine.Application.Queue;
using DigiLine.Application.Sessions;
using DigiLine.Application.Transactions;
using DigiLine.Infrastructure.Authentication;
using DigiLine.Infrastructure.Persistence;
using DigiLine.Infrastructure.Persistence.Repositories;
using DigiLine.Infrastructure.Persistence.Transactions;
using DigiLine.Infrastructure.Tenancy;
using DigiLine.Infrastructure.Patients;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;


namespace DigiLine.Infrastructure
{
    public static class DependencyInjection
    {
        public static IServiceCollection AddInfrastructure(
            this IServiceCollection services,
            IConfiguration configuration)
        {
            var connectionString =
                configuration.GetConnectionString("DigiLine");

            services.AddDbContext<DigiLineDbContext>(options =>
                options.UseSqlServer(connectionString));

            services.Configure<JwtSettings>(configuration.GetSection("JwtSettings"));

            services.AddScoped<ICurrentClinicService, CurrentClinicService>();

            services.AddScoped<IPasswordHasher, PasswordHasher>();

            services.AddScoped<IAuthenticationService, AuthenticationService>();
            services.AddScoped<DigiLine.Application.Administration.IAdministrationService, AdministrationService>();
            services.AddScoped<IStaffPasswordService, StaffPasswordService>();
            services.AddScoped<IStaffEmailSender, SmtpStaffEmailSender>();

            services.AddScoped<IStaffSessionQueries, StaffSessionQueries>();
            services.AddScoped<ISessionRepository, SessionRepository>();
            services.AddScoped<IPublicClinicRepository, PublicClinicRepository>();
            services.AddScoped<IClinicTimeZoneResolver, ClinicTimeZoneResolver>();

            services.AddScoped<IPatientProfileRepository, PatientProfileRepository>();
            services.AddScoped<IPublicPatientRepository, PublicPatientRepository>();
            services.AddSingleton<IPatientAccessService, InMemoryPatientAccessService>();

            services.AddScoped<IQueueTokenRepository, QueueTokenRepository>();

            services.AddScoped<ITokenNumberService, TokenNumberService>();

            services.AddScoped<ITransactionManager, EfTransactionManager>();


            return services;
        }
    }
}

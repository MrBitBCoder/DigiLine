using DigiLine.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DigiLine.Infrastructure.Persistence
{
    public class DigiLineDbContext : DbContext
    {
        public DigiLineDbContext(
            DbContextOptions<DigiLineDbContext> options)
            : base(options)
        {


        }
        public DbSet<Clinic> Clinics => Set<Clinic>();

        public DbSet<StaffInvitation> StaffInvitations => Set<StaffInvitation>();
        public DbSet<AdministrationAudit> AdministrationAudits => Set<AdministrationAudit>();
        public DbSet<Staff> Staff => Set<Staff>();
        public DbSet<PasswordResetToken> PasswordResetTokens => Set<PasswordResetToken>();

        public DbSet<PhoneAccount> PhoneAccounts => Set<PhoneAccount>();

        public DbSet<PatientProfile> PatientProfiles => Set<PatientProfile>();

        public DbSet<Session> Sessions => Set<Session>();

        public DbSet<QueueToken> QueueTokens => Set<QueueToken>();



        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // SQL Server datetime2 does not retain DateTime.Kind. These values are
            // stored as UTC; restore that metadata so JSON includes the Z suffix.
            var utcDateTime = new ValueConverter<DateTime, DateTime>(
                value => RequireUtc(value),
                value => DateTime.SpecifyKind(value, DateTimeKind.Utc));


            modelBuilder.ApplyConfigurationsFromAssembly(typeof(DigiLineDbContext).Assembly);


            modelBuilder.Entity<Clinic>(entity =>
            {
                entity.HasKey(x => x.Id);

                entity.Property(x => x.Name)
                    .IsRequired()
                    .HasMaxLength(200);

                entity.Property(x => x.Slug)
                    .IsRequired()
                    .HasMaxLength(100);

                entity.Property(x => x.TimeZoneId).IsRequired().HasMaxLength(100);

                entity.HasIndex(x => x.Slug)
                    .IsUnique();

                entity.Property(x => x.CreatedAt)
                    .IsRequired();

                entity.Property(x => x.UpdatedAt)
                    .IsRequired();
            });


            modelBuilder.Entity<Staff>(entity =>
            {
                entity.HasKey(x => x.Id);

                entity.Property(x => x.Name)
                    .IsRequired()
                    .HasMaxLength(200);

                entity.Property(x => x.Email)
                    .IsRequired()
                    .HasMaxLength(320);
                entity.Property(x => x.NormalizedEmail).IsRequired().HasMaxLength(320);
                entity.HasIndex(x => x.NormalizedEmail).IsUnique();
                entity.Property(x => x.PasswordVersion).IsRequired().IsConcurrencyToken();

                entity.Property(x => x.PasswordHash)
                    .IsRequired()
                    .HasMaxLength(500);

                entity.Property(x => x.Role).IsRequired();
                entity.Property(x => x.IsActive).HasDefaultValue(true);
                entity.HasMany(x => x.Roles).WithOne().HasForeignKey(x => x.StaffId).OnDelete(DeleteBehavior.Cascade);

                entity.Property(x => x.CreatedAt)
                    .IsRequired();

                entity.Property(x => x.UpdatedAt)
                    .IsRequired();

                entity.HasOne(x => x.Clinic)
                    .WithMany()
                    .HasForeignKey(x => x.ClinicId)
                    .OnDelete(DeleteBehavior.Restrict);

                entity.HasIndex(x => new { x.ClinicId, x.Email })
                    .IsUnique();
            });


            modelBuilder.Entity<PhoneAccount>(entity =>
            {
                entity.HasKey(x => x.Id);

                entity.Property(x => x.PhoneNumber)
                    .IsRequired()
                    .HasMaxLength(20);

                entity.HasIndex(x => x.PhoneNumber)
                    .IsUnique();

                entity.Property(x => x.CreatedAt)
                    .IsRequired();

                entity.Property(x => x.UpdatedAt)
                    .IsRequired();
            });


            modelBuilder.Entity<PatientProfile>(entity =>
            {
                entity.HasKey(x => x.Id);

                entity.Property(x => x.Name)
                    .IsRequired()
                    .HasMaxLength(200);

                entity.Property(x => x.Age)
                    .IsRequired();

                entity.Property(x => x.CreatedAt)
                    .IsRequired();

                entity.Property(x => x.UpdatedAt)
                    .IsRequired();

                entity.HasOne(x => x.PhoneAccount)
                    .WithMany()
                    .HasForeignKey(x => x.PhoneAccountId)
                    .OnDelete(DeleteBehavior.Restrict);

                entity.HasIndex(x => x.PhoneAccountId);
            });

            modelBuilder.Entity<Session>(entity =>
            {
                entity.HasKey(x => x.Id);

                entity.Property(x => x.Name)
                    .IsRequired()
                    .HasMaxLength(200);

                entity.Property(x => x.BookingOpenTime)
                    .IsRequired().HasConversion(utcDateTime);

                entity.Property(x => x.StartTime)
                    .IsRequired().HasConversion(utcDateTime);

                entity.Property(x => x.EndTime)
                    .IsRequired().HasConversion(utcDateTime);

                entity.Property(x => x.LastBookingTime)
                    .IsRequired().HasConversion(utcDateTime);

                entity.Property(x => x.NextTokenNumber)
                    .IsRequired();

                entity.Property(x => x.NextQueueOrder).IsRequired();

                entity.Property(x => x.AverageConsultationMinutes)
                    .IsRequired();

                entity.Property(x => x.Status)
                    .IsRequired();

                entity.Property(x => x.CreatedAt)
                    .IsRequired();

                entity.Property(x => x.UpdatedAt)
                    .IsRequired();

                entity.HasOne(x => x.Clinic)
                    .WithMany()
                    .HasForeignKey(x => x.ClinicId)
                    .OnDelete(DeleteBehavior.Restrict);

                entity.HasOne(x => x.Doctor)
                    .WithMany()
                    .HasForeignKey(x => x.DoctorId)
                    .OnDelete(DeleteBehavior.Restrict);

                entity.HasIndex(x => x.ClinicId);

                entity.HasIndex(x => x.DoctorId);
            });

            modelBuilder.Entity<PasswordResetToken>(entity =>
            {
                entity.HasKey(x => x.Id);
                entity.Property(x => x.TokenHash).IsRequired().HasMaxLength(64);
                entity.Property(x => x.UsedAt).IsConcurrencyToken();
                entity.HasIndex(x => x.TokenHash).IsUnique();
                entity.HasIndex(x => new { x.StaffId, x.CreatedAt });
                entity.HasOne<Staff>().WithMany().HasForeignKey(x => x.StaffId).OnDelete(DeleteBehavior.Cascade);
            });

            modelBuilder.Entity<StaffRoleAssignment>().HasKey(x => new { x.StaffId, x.Role });
            modelBuilder.Entity<StaffInvitation>(entity =>
            {
                entity.HasKey(x => x.Id);
                entity.Property(x => x.TokenHash).HasMaxLength(64);
                entity.HasIndex(x => x.TokenHash).IsUnique();
                entity.Property(x => x.UsedAt).IsConcurrencyToken();
                entity.HasOne<Staff>().WithMany().HasForeignKey(x => x.StaffId);
            });
            modelBuilder.Entity<AdministrationAudit>(entity =>
            {
                entity.HasKey(x => x.Id);
                entity.Property(x => x.Action).HasMaxLength(200);
                entity.HasIndex(x => new { x.ClinicId, x.OccurredAt });
            });
            modelBuilder.Entity<Session>().HasIndex(x => new { x.ClinicId, x.DoctorId, x.StartTime, x.Id });
            modelBuilder.Entity<Session>().HasIndex(x => new { x.ClinicId, x.StartTime, x.Id });
            modelBuilder.Entity<QueueToken>().HasIndex(x => new { x.SessionId, x.Status, x.QueueOrder, x.TokenNumber });

            // Apply the same UTC contract to audit and queue-event timestamps.
            foreach (var entityType in modelBuilder.Model.GetEntityTypes())
                foreach (var property in entityType.GetProperties())
                {
                    if (property.ClrType == typeof(DateTime))
                        property.SetValueConverter(utcDateTime);
                    if (property.ClrType == typeof(DateTime?))
                        property.SetValueConverter(new ValueConverter<DateTime?, DateTime?>(
                            value => value.HasValue ? RequireUtc(value.Value) : null,
                            value => value.HasValue ? DateTime.SpecifyKind(value.Value, DateTimeKind.Utc) : null));
                }


        }

        private static DateTime RequireUtc(DateTime value) =>
            value.Kind == DateTimeKind.Utc ? value : throw new InvalidOperationException("Persisted timestamps must be UTC.");


    }
}

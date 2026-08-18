using DigiLine.Domain.Entities;
using Microsoft.EntityFrameworkCore;
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

        public DbSet<Staff> Staff => Set<Staff>();

        public DbSet<PhoneAccount> PhoneAccounts => Set<PhoneAccount>();

        public DbSet<PatientProfile> PatientProfiles => Set<PatientProfile>();

        public DbSet<Session> Sessions => Set<Session>();

        public DbSet<QueueToken> QueueTokens => Set<QueueToken>();



        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

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

                entity.Property(x => x.PasswordHash)
                    .IsRequired()
                    .HasMaxLength(500);

                entity.Property(x => x.Role)
                    .IsRequired();

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
                    .IsRequired();

                entity.Property(x => x.StartTime)
                    .IsRequired();

                entity.Property(x => x.EndTime)
                    .IsRequired();

                entity.Property(x => x.LastBookingTime)
                    .IsRequired();

                entity.Property(x => x.NextTokenNumber)
                    .IsRequired();

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

           
        }


    }
}

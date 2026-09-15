using DigiLine.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DigiLine.Infrastructure.Persistence.Configurations
{
    public class QueueTokenConfiguration : IEntityTypeConfiguration<QueueToken>
    {
        public void Configure(EntityTypeBuilder<QueueToken> builder)
        {
            builder.HasKey(x => x.Id);

            builder.Property(x => x.TokenNumber)
                .IsRequired();

            builder.Property(x => x.WalkInName)
                .HasMaxLength(200);

            builder.Property(x => x.PhoneNumber)
                .HasMaxLength(20);

            builder.Property(x => x.Status)
                .IsRequired();

            builder.Property(x => x.CreatedAt)
                .IsRequired();

            builder.HasIndex(x => x.SessionId);
            builder.HasIndex(x => new { x.SessionId, x.QueueOrder }).IsUnique();

            builder.HasIndex(x => new
            {
                x.SessionId,
                x.TokenNumber
            })
            .IsUnique();

            builder.HasIndex(x => x.PatientProfileId);

            builder.HasOne(x => x.Session)
                  .WithMany(x => x.QueueTokens)
                  .HasForeignKey(x => x.SessionId)
                  .OnDelete(DeleteBehavior.Restrict);

            builder.HasOne(x => x.PatientProfile)
                .WithMany()
                .HasForeignKey(x => x.PatientProfileId)
                .OnDelete(DeleteBehavior.Restrict);
        }
    }
}

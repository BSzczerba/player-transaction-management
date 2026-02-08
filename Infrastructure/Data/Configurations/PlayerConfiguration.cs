using Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Data.Configurations;

public class PlayerConfiguration : IEntityTypeConfiguration<Player>
{
    public void Configure(EntityTypeBuilder<Player> builder)
    {
        builder.ToTable("Players");

        builder.HasKey(p => p.Id);

        builder.Property(p => p.Email)
            .IsRequired()
            .HasMaxLength(255);

        builder.HasIndex(p => p.Email)
            .IsUnique();

        builder.Property(p => p.Username)
            .IsRequired()
            .HasMaxLength(100);

        builder.HasIndex(p => p.Username)
            .IsUnique();

        builder.Property(p => p.PasswordHash)
            .IsRequired()
            .HasMaxLength(500);

        builder.Property(p => p.FirstName)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(p => p.LastName)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(p => p.PhoneNumber)
            .HasMaxLength(20);

        builder.Property(p => p.Balance)
            .HasPrecision(18, 2)
            .HasDefaultValue(0);

        builder.Property(p => p.DailyDepositLimit)
            .HasPrecision(18, 2)
            .HasDefaultValue(10000);

        builder.Property(p => p.DailyWithdrawalLimit)
            .HasPrecision(18, 2)
            .HasDefaultValue(5000);

        builder.Property(p => p.Role)
            .HasConversion<string>()
            .HasMaxLength(50);

        builder.Property(p => p.Status)
            .HasConversion<string>()
            .HasMaxLength(50);

        // Relationships
        builder.HasMany(p => p.Transactions)
            .WithOne(t => t.Player)
            .HasForeignKey(t => t.PlayerId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasMany(p => p.AuditLogs)
            .WithOne(a => a.User)
            .HasForeignKey(a => a.UserId)
            .OnDelete(DeleteBehavior.SetNull);

        // Indexes for performance
        builder.HasIndex(p => p.Status);
        builder.HasIndex(p => p.CreatedAt);
    }
}
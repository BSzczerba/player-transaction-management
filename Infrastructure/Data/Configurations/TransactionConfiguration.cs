using Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Data.Configurations;

public class TransactionConfiguration : IEntityTypeConfiguration<Transaction>
{
    public void Configure(EntityTypeBuilder<Transaction> builder)
    {
        builder.ToTable("Transactions");

        builder.HasKey(t => t.Id);

        builder.Property(t => t.Amount)
            .IsRequired()
            .HasPrecision(18, 2);

        builder.Property(t => t.Type)
            .IsRequired()
            .HasConversion<string>()
            .HasMaxLength(50);

        builder.Property(t => t.Status)
            .IsRequired()
            .HasConversion<string>()
            .HasMaxLength(50);

        builder.Property(t => t.PaymentGatewayReference)
            .HasMaxLength(255);

        builder.Property(t => t.Description)
            .HasMaxLength(1000);

        builder.Property(t => t.RejectionReason)
            .HasMaxLength(1000);

        builder.Property(t => t.IpAddress)
            .HasMaxLength(50);

        builder.Property(t => t.FlagReason)
            .HasMaxLength(1000);

        builder.Property(t => t.BalanceBefore)
            .HasPrecision(18, 2);

        builder.Property(t => t.BalanceAfter)
            .HasPrecision(18, 2);

        // Relationships
        builder.HasOne(t => t.Player)
            .WithMany(p => p.Transactions)
            .HasForeignKey(t => t.PlayerId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(t => t.PaymentMethod)
            .WithMany(pm => pm.Transactions)
            .HasForeignKey(t => t.PaymentMethodId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasOne(t => t.ApprovedBy)
            .WithMany()
            .HasForeignKey(t => t.ApprovedById)
            .OnDelete(DeleteBehavior.SetNull);

        // Indexes for performance
        builder.HasIndex(t => t.PlayerId);
        builder.HasIndex(t => t.Status);
        builder.HasIndex(t => t.Type);
        builder.HasIndex(t => t.CreatedAt);
        builder.HasIndex(t => t.IsFlagged);
        builder.HasIndex(t => new { t.PlayerId, t.CreatedAt });
    }
}
using Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Data.Configurations;

public class PaymentMethodConfiguration : IEntityTypeConfiguration<PaymentMethod>
{
    public void Configure(EntityTypeBuilder<PaymentMethod> builder)
    {
        builder.ToTable("PaymentMethods");

        builder.HasKey(pm => pm.Id);

        builder.Property(pm => pm.Name)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(pm => pm.Type)
            .IsRequired()
            .HasConversion<string>()
            .HasMaxLength(50);

        builder.Property(pm => pm.MinAmount)
            .HasPrecision(18, 2)
            .HasDefaultValue(10);

        builder.Property(pm => pm.MaxAmount)
            .HasPrecision(18, 2)
            .HasDefaultValue(100000);

        builder.Property(pm => pm.FeePercentage)
            .HasPrecision(5, 2)
            .HasDefaultValue(0);

        builder.Property(pm => pm.FixedFee)
            .HasPrecision(18, 2)
            .HasDefaultValue(0);

        builder.Property(pm => pm.ProcessingTimeMinutes)
            .HasDefaultValue(0);

        builder.Property(pm => pm.LogoUrl)
            .HasMaxLength(500);

        // Indexes
        builder.HasIndex(pm => pm.Type);
        builder.HasIndex(pm => pm.IsActive);
    }
}
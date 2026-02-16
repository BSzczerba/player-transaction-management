using Domain.Entities;
using Domain.Enums;
using Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Data;

/// <summary>
/// Seeds initial data into the database
/// </summary>
public static class DatabaseSeeder
{
    /// <summary>
    /// Seed initial data
    /// </summary>
    public static async Task SeedAsync(ApplicationDbContext context)
    {
        // Check if already seeded
        if (await context.Players.AnyAsync() || await context.PaymentMethods.AnyAsync())
        {
            return; // Database already seeded
        }

        await SeedPaymentMethodsAsync(context);
        await SeedTestUsersAsync(context);

        await context.SaveChangesAsync();
    }

    private static async Task SeedPaymentMethodsAsync(ApplicationDbContext context)
    {
        var paymentMethods = new List<PaymentMethod>
        {
            new PaymentMethod
            {
                Id = Guid.NewGuid(),
                Name = "Visa/Mastercard",
                Type = PaymentMethodType.CreditCard,
                IsActive = true,
                MinAmount = 10,
                MaxAmount = 50000,
                FeePercentage = 2.5m,
                FixedFee = 0,
                ProcessingTimeMinutes = 0, // Instant
                CreatedAt = DateTime.UtcNow
            },
            new PaymentMethod
            {
                Id = Guid.NewGuid(),
                Name = "Bank Transfer",
                Type = PaymentMethodType.BankTransfer,
                IsActive = true,
                MinAmount = 50,
                MaxAmount = 100000,
                FeePercentage = 0,
                FixedFee = 5,
                ProcessingTimeMinutes = 1440, // 24 hours
                CreatedAt = DateTime.UtcNow
            },
            new PaymentMethod
            {
                Id = Guid.NewGuid(),
                Name = "PayPal",
                Type = PaymentMethodType.PayPal,
                IsActive = true,
                MinAmount = 10,
                MaxAmount = 10000,
                FeePercentage = 3.0m,
                FixedFee = 0.30m,
                ProcessingTimeMinutes = 0, // Instant
                CreatedAt = DateTime.UtcNow
            },
            new PaymentMethod
            {
                Id = Guid.NewGuid(),
                Name = "Skrill",
                Type = PaymentMethodType.Skrill,
                IsActive = true,
                MinAmount = 10,
                MaxAmount = 10000,
                FeePercentage = 1.9m,
                FixedFee = 0,
                ProcessingTimeMinutes = 0, // Instant
                CreatedAt = DateTime.UtcNow
            },
            new PaymentMethod
            {
                Id = Guid.NewGuid(),
                Name = "Neteller",
                Type = PaymentMethodType.Neteller,
                IsActive = true,
                MinAmount = 10,
                MaxAmount = 10000,
                FeePercentage = 2.0m,
                FixedFee = 0,
                ProcessingTimeMinutes = 0, // Instant
                CreatedAt = DateTime.UtcNow
            },
            new PaymentMethod
            {
                Id = Guid.NewGuid(),
                Name = "Bitcoin",
                Type = PaymentMethodType.Cryptocurrency,
                IsActive = true,
                MinAmount = 20,
                MaxAmount = 50000,
                FeePercentage = 1.0m,
                FixedFee = 0,
                ProcessingTimeMinutes = 60, // ~1 hour for confirmation
                CreatedAt = DateTime.UtcNow
            }
        };

        await context.PaymentMethods.AddRangeAsync(paymentMethods);
    }

    private static async Task SeedTestUsersAsync(ApplicationDbContext context)
    {
        // Password for all test users: "TestPass123!"
        var passwordHash = BCrypt.Net.BCrypt.HashPassword("TestPass123!");

        var testUsers = new List<Player>
        {
            // Test Player
            new Player
            {
                Id = Guid.NewGuid(),
                Email = "player@test.com",
                Username = "testplayer",
                PasswordHash = passwordHash,
                FirstName = "Test",
                LastName = "Player",
                PhoneNumber = "+1234567890",
                DateOfBirth = new DateTime(1990, 1, 1),
                Role = UserRole.Player,
                Status = AccountStatus.Active,
                Balance = 1000.00m,
                DailyDepositLimit = 10000,
                DailyWithdrawalLimit = 5000,
                EmailVerified = true,
                PhoneVerified = true,
                KycVerified = true,
                CreatedAt = DateTime.UtcNow
            },

            // Test Operator
            new Player
            {
                Id = Guid.NewGuid(),
                Email = "operator@test.com",
                Username = "testoperator",
                PasswordHash = passwordHash,
                FirstName = "Test",
                LastName = "Operator",
                Role = UserRole.Operator,
                Status = AccountStatus.Active,
                Balance = 0,
                EmailVerified = true,
                KycVerified = true,
                CreatedAt = DateTime.UtcNow
            },

            // Test Administrator
            new Player
            {
                Id = Guid.NewGuid(),
                Email = "admin@test.com",
                Username = "testadmin",
                PasswordHash = passwordHash,
                FirstName = "Test",
                LastName = "Administrator",
                Role = UserRole.Administrator,
                Status = AccountStatus.Active,
                Balance = 0,
                EmailVerified = true,
                KycVerified = true,
                CreatedAt = DateTime.UtcNow
            },

            // Test Compliance Officer
            new Player
            {
                Id = Guid.NewGuid(),
                Email = "compliance@test.com",
                Username = "testcompliance",
                PasswordHash = passwordHash,
                FirstName = "Test",
                LastName = "Compliance",
                Role = UserRole.ComplianceOfficer,
                Status = AccountStatus.Active,
                Balance = 0,
                EmailVerified = true,
                KycVerified = true,
                CreatedAt = DateTime.UtcNow
            }
        };

        await context.Players.AddRangeAsync(testUsers);
    }
}

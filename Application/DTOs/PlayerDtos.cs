using Domain.Enums;

namespace Application.DTOs;

/// <summary>
/// DTO for player information
/// </summary>
public class PlayerDto
{
    public Guid Id { get; set; }
    public string Email { get; set; } = string.Empty;
    public string Username { get; set; } = string.Empty;
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string? PhoneNumber { get; set; }
    public DateTime? DateOfBirth { get; set; }
    public string Role { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public decimal Balance { get; set; }
    public decimal DailyDepositLimit { get; set; }
    public decimal DailyWithdrawalLimit { get; set; }
    public bool EmailVerified { get; set; }
    public bool PhoneVerified { get; set; }
    public bool KycVerified { get; set; }
    public DateTime? LastLoginAt { get; set; }
    public DateTime CreatedAt { get; set; }
}

/// <summary>
/// DTO for updating player profile
/// </summary>
public class UpdatePlayerDto
{
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string? PhoneNumber { get; set; }
    public DateTime? DateOfBirth { get; set; }
}

/// <summary>
/// DTO for updating player limits
/// </summary>
public class UpdatePlayerLimitsDto
{
    public decimal DailyDepositLimit { get; set; }
    public decimal DailyWithdrawalLimit { get; set; }
}
namespace Application.DTOs;

/// <summary>
/// DTO for user registration
/// </summary>
public class RegisterDto
{
    public string Email { get; set; } = string.Empty;
    public string Username { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public string ConfirmPassword { get; set; } = string.Empty;
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public DateTime? DateOfBirth { get; set; }
    public string? PhoneNumber { get; set; }
}

/// <summary>
/// DTO for user login
/// </summary>
public class LoginDto
{
    public string EmailOrUsername { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
}

/// <summary>
/// DTO for authentication response
/// </summary>
public class AuthResponseDto
{
    public string Token { get; set; } = string.Empty;
    public string RefreshToken { get; set; } = string.Empty;
    public DateTime ExpiresAt { get; set; }
    public PlayerDto User { get; set; } = null!;
    /// <summary>
    /// Activation token returned on registration — simulate email delivery.
    /// In production this would be sent via email, not returned in the response.
    /// </summary>
    public string? ActivationToken { get; set; }
}

/// <summary>
/// DTO for refresh token request
/// </summary>
public class RefreshTokenDto
{
    public string Token { get; set; } = string.Empty;
    public string RefreshToken { get; set; } = string.Empty;
}

/// <summary>
/// DTO for account activation via token
/// </summary>
public class ActivateAccountDto
{
    public string Token { get; set; } = string.Empty;
}
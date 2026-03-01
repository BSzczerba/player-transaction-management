using Domain.Enums;

namespace Application.DTOs;

public class UpdateRoleDto
{
    public UserRole Role { get; set; }
}

public class SetKycDto
{
    public bool Verified { get; set; }
}

public class ClearFlagDto
{
    public string Notes { get; set; } = string.Empty;
}

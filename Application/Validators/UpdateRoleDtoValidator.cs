using Application.DTOs;
using FluentValidation;

namespace Application.Validators;

public class UpdateRoleDtoValidator : AbstractValidator<UpdateRoleDto>
{
    public UpdateRoleDtoValidator()
    {
        RuleFor(x => x.Role)
            .IsInEnum().WithMessage("Invalid role value");
    }
}

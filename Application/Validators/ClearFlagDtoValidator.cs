using Application.DTOs;
using FluentValidation;

namespace Application.Validators;

public class ClearFlagDtoValidator : AbstractValidator<ClearFlagDto>
{
    public ClearFlagDtoValidator()
    {
        RuleFor(x => x.Notes)
            .NotEmpty().WithMessage("Notes are required when clearing an AML flag")
            .MinimumLength(10).WithMessage("Notes must be at least 10 characters")
            .MaximumLength(1000).WithMessage("Notes must not exceed 1000 characters");
    }
}

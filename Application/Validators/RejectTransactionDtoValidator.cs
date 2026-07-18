using Application.DTOs;
using FluentValidation;

namespace Application.Validators;

/// <summary>
/// Validator for RejectTransactionDto
/// </summary>
public class RejectTransactionDtoValidator : AbstractValidator<RejectTransactionDto>
{
    public RejectTransactionDtoValidator()
    {
        RuleFor(x => x.Reason)
            .NotEmpty().WithMessage("Rejection reason is required")
            .MinimumLength(10).WithMessage("Rejection reason must be at least 10 characters")
            .MaximumLength(1000).WithMessage("Rejection reason must not exceed 1000 characters");
    }
}

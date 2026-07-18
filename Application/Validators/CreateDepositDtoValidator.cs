using Application.DTOs;
using FluentValidation;

namespace Application.Validators;

/// <summary>
/// Validator for CreateDepositDto
/// </summary>
public class CreateDepositDtoValidator : AbstractValidator<CreateDepositDto>
{
    public CreateDepositDtoValidator()
    {
        RuleFor(x => x.Amount)
            .GreaterThan(0).WithMessage("Amount must be greater than 0")
            .LessThanOrEqualTo(1000000).WithMessage("Amount cannot exceed 1,000,000");

        RuleFor(x => x.PaymentMethodId)
            .NotEmpty().WithMessage("Payment method is required");

        RuleFor(x => x.Description)
            .MaximumLength(500).When(x => !string.IsNullOrEmpty(x.Description))
            .WithMessage("Description must not exceed 500 characters");
    }
}

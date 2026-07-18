using Application.DTOs;
using FluentValidation;

namespace Application.Validators;

public class UpdatePlayerLimitsDtoValidator : AbstractValidator<UpdatePlayerLimitsDto>
{
    public UpdatePlayerLimitsDtoValidator()
    {
        RuleFor(x => x.DailyDepositLimit)
            .GreaterThan(0).WithMessage("Daily deposit limit must be greater than 0")
            .LessThanOrEqualTo(1000000).WithMessage("Daily deposit limit cannot exceed 1,000,000");

        RuleFor(x => x.DailyWithdrawalLimit)
            .GreaterThan(0).WithMessage("Daily withdrawal limit must be greater than 0")
            .LessThanOrEqualTo(500000).WithMessage("Daily withdrawal limit cannot exceed 500,000");
    }
}

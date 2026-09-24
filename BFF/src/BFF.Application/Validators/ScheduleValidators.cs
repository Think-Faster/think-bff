using BFF.Contracts.Schedule;
using FluentValidation;

namespace BFF.Application.Validators;

public sealed class CreateScheduleEntryRequestValidator : AbstractValidator<CreateScheduleEntryRequest>
{
    public CreateScheduleEntryRequestValidator()
    {
        RuleFor(x => x.DateTo).GreaterThanOrEqualTo(x => x.DateFrom);
    }
}

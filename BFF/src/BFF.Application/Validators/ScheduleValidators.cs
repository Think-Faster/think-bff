using BFF.Contracts.Schedule;
using FluentValidation;

namespace BFF.Application.Validators;

public sealed class CreateScheduleEntryRequestValidator : AbstractValidator<CreateScheduleEntryRequest>
{
    public CreateScheduleEntryRequestValidator()
    {
        RuleFor(x => x.DateTo).GreaterThanOrEqualTo(x => x.DateFrom);
        RuleFor(x => x.ShiftHours).InclusiveBetween((short)1, (short)24).When(x => x.ShiftHours is not null);
        RuleFor(x => x.ShiftHours).NotNull().When(x => x.ShiftStart is not null)
            .WithMessage("ShiftHours is required when ShiftStart is set.");
    }
}

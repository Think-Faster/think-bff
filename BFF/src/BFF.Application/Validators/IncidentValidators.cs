using BFF.Contracts.Incidents;
using FluentValidation;

namespace BFF.Application.Validators;

public sealed class CreateIncidentRequestValidator : AbstractValidator<CreateIncidentRequest>
{
    public CreateIncidentRequestValidator()
    {
        RuleFor(x => x.ObjectId).GreaterThan(0);
    }
}

public sealed class ConfirmIncidentRequestValidator : AbstractValidator<ConfirmIncidentRequest>
{
    public ConfirmIncidentRequestValidator()
    {
        RuleFor(x => x.Outcome).MaximumLength(2000);
    }
}

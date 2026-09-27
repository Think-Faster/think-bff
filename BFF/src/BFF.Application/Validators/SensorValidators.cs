using BFF.Contracts.Sensors;
using FluentValidation;

namespace BFF.Application.Validators;

public sealed class CreateSensorRequestValidator : AbstractValidator<CreateSensorRequest>
{
    public CreateSensorRequestValidator()
    {
        RuleFor(x => x.Id).GreaterThan(0);
        RuleFor(x => x.ObjectId).GreaterThan(0);
        RuleFor(x => x.System).NotEmpty().MaximumLength(100);
        RuleFor(x => x.SType).NotEmpty().MaximumLength(100);
        RuleFor(x => x.Name).NotEmpty().MaximumLength(200);
    }
}

public sealed class UpdateSensorRequestValidator : AbstractValidator<UpdateSensorRequest>
{
    public UpdateSensorRequestValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(200);
    }
}

public sealed class CreateSensorLinkRequestValidator : AbstractValidator<CreateSensorLinkRequest>
{
    public CreateSensorLinkRequestValidator()
    {
        RuleFor(x => x.ToSensorId).GreaterThan(0);
        RuleFor(x => x.Kind).NotEmpty().MaximumLength(50);
    }
}

using BFF.Contracts.ModelSettings;
using BFF.Models.Enums;
using FluentValidation;

namespace BFF.Application.Validators;

public sealed class CreateModelVersionRequestValidator : AbstractValidator<CreateModelVersionRequest>
{
    public CreateModelVersionRequestValidator()
    {
        RuleFor(x => x.Id).NotEmpty().MaximumLength(100);
        RuleFor(x => x.Name).NotEmpty().MaximumLength(200);
    }
}

public sealed class CreateCoefficientRequestValidator : AbstractValidator<CreateCoefficientRequest>
{
    public CreateCoefficientRequestValidator()
    {
        RuleFor(x => x.Share).InclusiveBetween(0, 1);
        RuleFor(x => x.RejectK).InclusiveBetween(0, 1).When(x => x.RejectK.HasValue);
    }
}

public sealed class CreateRetrainJobRequestValidator : AbstractValidator<CreateRetrainJobRequest>
{
    public CreateRetrainJobRequestValidator()
    {
        RuleFor(x => x.ParamsJson).MaximumLength(10_000);
    }
}

public sealed class CreateIgnoredRangeRequestValidator : AbstractValidator<CreateIgnoredRangeRequest>
{
    public CreateIgnoredRangeRequestValidator()
    {
        RuleFor(x => x.DateTo).GreaterThanOrEqualTo(x => x.DateFrom);
        RuleFor(x => x.Reason).NotEmpty().MaximumLength(500);
        RuleFor(x => x.ObjectId).NotNull().When(x => x.Scope == IgnoredRangeScope.Object)
            .WithMessage("objectId is required when scope is 'object'.");
        RuleFor(x => x.SensorId).NotNull().When(x => x.Scope == IgnoredRangeScope.Sensor)
            .WithMessage("sensorId is required when scope is 'sensor'.");
    }
}

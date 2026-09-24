using BFF.Contracts.Objects;
using FluentValidation;

namespace BFF.Application.Validators;

public sealed class CreateObjectRequestValidator : AbstractValidator<CreateObjectRequest>
{
    public CreateObjectRequestValidator()
    {
        RuleFor(x => x.Id).GreaterThan(0);
        RuleFor(x => x.Kind).NotEmpty().MaximumLength(100);
        RuleFor(x => x.Name).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Address).MaximumLength(500);
    }
}

public sealed class UpdateObjectRequestValidator : AbstractValidator<UpdateObjectRequest>
{
    public UpdateObjectRequestValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Address).MaximumLength(500);
    }
}

public sealed class CreatePicketRequestValidator : AbstractValidator<CreatePicketRequest>
{
    public CreatePicketRequestValidator()
    {
        RuleFor(x => x.Code).NotEmpty().MaximumLength(50);
    }
}

public sealed class UpdatePicketRequestValidator : AbstractValidator<UpdatePicketRequest>
{
    public UpdatePicketRequestValidator()
    {
        RuleFor(x => x.Ordinal).GreaterThanOrEqualTo(0);
    }
}

public sealed class UpsertMapLayerRequestValidator : AbstractValidator<UpsertMapLayerRequest>
{
    public UpsertMapLayerRequestValidator()
    {
        RuleFor(x => x.Level).InclusiveBetween((short)1, (short)4);
        RuleFor(x => x.Kind).NotEmpty().MaximumLength(50);
        RuleFor(x => x.GeoJson).NotEmpty();
    }
}

using BFF.Contracts.Engineers;
using FluentValidation;

namespace BFF.Application.Validators;

public sealed class CreateBrigadeRequestValidator : AbstractValidator<CreateBrigadeRequest>
{
    public CreateBrigadeRequestValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(200);
    }
}

public sealed class UpsertEngineerProfileRequestValidator : AbstractValidator<UpsertEngineerProfileRequest>
{
    public UpsertEngineerProfileRequestValidator()
    {
        RuleFor(x => x.Phone).MaximumLength(30);
        RuleFor(x => x.Telegram).MaximumLength(100);
    }
}

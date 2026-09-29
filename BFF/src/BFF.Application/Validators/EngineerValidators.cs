using BFF.Contracts.Engineers;
using BFF.Models.Constants;
using BFF.Models.Enums;
using FluentValidation;

namespace BFF.Application.Validators;

public sealed class CreateBrigadeRequestValidator : AbstractValidator<CreateBrigadeRequest>
{
    public CreateBrigadeRequestValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Unit).MaximumLength(200);
    }
}

public sealed class UpsertEngineerProfileRequestValidator : AbstractValidator<UpsertEngineerProfileRequest>
{
    public UpsertEngineerProfileRequestValidator()
    {
        RuleFor(x => x.Phone).MaximumLength(30);
        RuleFor(x => x.Telegram).Must(TelegramUsername.IsValidOrEmpty)
            .WithMessage("telegram must be a Telegram username: 5-32 Latin letters, digits or _, starting with a letter.");
    }
}

public sealed class CreateEngineerPermitRequestValidator : AbstractValidator<CreateEngineerPermitRequest>
{
    public CreateEngineerPermitRequestValidator()
    {
        RuleFor(x => x.Kind).IsInEnum();
        // Confined space: group 1-3; electrical safety: group 2-5; gas hazard work has no group.
        RuleFor(x => x.Level).NotNull().InclusiveBetween((short)1, (short)3).When(x => x.Kind == PermitKind.ConfinedSpace);
        RuleFor(x => x.Level).NotNull().InclusiveBetween((short)2, (short)5).When(x => x.Kind == PermitKind.Electrical);
        RuleFor(x => x.Level).Null().When(x => x.Kind == PermitKind.GasHazard);
        RuleFor(x => x.DocumentNo).MaximumLength(100);
        RuleFor(x => x.CheckedAt).LessThanOrEqualTo(x => x.ValidUntil).When(x => x.CheckedAt is not null);
    }
}

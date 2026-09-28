using BFF.Contracts.Predictions;
using BFF.Models.Enums;
using FluentValidation;

namespace BFF.Application.Validators;

public sealed class CreatePredictionRequestValidator : AbstractValidator<CreatePredictionRequest>
{
    public CreatePredictionRequestValidator()
    {
        RuleFor(x => x.ObjectId).GreaterThan(0);
        RuleFor(x => x.Topic).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Score).InclusiveBetween(0, 1);
        RuleFor(x => x.Probability).InclusiveBetween(0, 1);
        RuleFor(x => x.Confidence).InclusiveBetween(0, 1);
        RuleFor(x => x.HorizonHours).GreaterThan((short)0);
    }
}

public sealed class CreatePredictionDecisionRequestValidator : AbstractValidator<CreatePredictionDecisionRequest>
{
    public CreatePredictionDecisionRequestValidator()
    {
        // "обязателен при REJECT" — раздел 3.3 исходного домен-документа.
        RuleFor(x => x.ReasonCode)
            .NotEmpty()
            .When(x => x.Action == DecisionAction.Reject)
            .WithMessage("reasonCode is required when rejecting a prediction.");
        RuleFor(x => x.ReasonCode).MaximumLength(50);

        // Справочник причин: «другое» — только с комментарием.
        RuleFor(x => x.Comment)
            .NotEmpty()
            .When(x => x.Action == DecisionAction.Reject && x.ReasonCode == "other")
            .WithMessage("comment is required when the reason is 'other'.");

        // Модель без срока молчание не примет (ML/INTEGRATION.md §13.3).
        RuleFor(x => x.Until)
            .NotNull()
            .When(x => x.Action == DecisionAction.Mute)
            .WithMessage("until is required when muting a prediction.");
        RuleFor(x => x.Until)
            .Must(until => until > DateTimeOffset.UtcNow.AddHours(1))
            .When(x => x.Action == DecisionAction.Mute && x.Until is not null)
            .WithMessage("until must be at least an hour ahead.");
    }
}

public sealed class CreateFactAlertRequestValidator : AbstractValidator<CreateFactAlertRequest>
{
    public CreateFactAlertRequestValidator()
    {
        RuleFor(x => x.ObjectId).GreaterThan(0);
    }
}

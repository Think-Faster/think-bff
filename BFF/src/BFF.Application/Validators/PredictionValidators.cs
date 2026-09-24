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
    }
}

public sealed class CreateFactAlertRequestValidator : AbstractValidator<CreateFactAlertRequest>
{
    public CreateFactAlertRequestValidator()
    {
        RuleFor(x => x.ObjectId).GreaterThan(0);
    }
}

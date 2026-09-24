using BFF.Contracts.Tasks;
using FluentValidation;

namespace BFF.Application.Validators;

public sealed class CreateWorkTaskRequestValidator : AbstractValidator<CreateWorkTaskRequest>
{
    public CreateWorkTaskRequestValidator()
    {
        RuleFor(x => x.Number).NotEmpty().MaximumLength(50);
        RuleFor(x => x.ObjectId).GreaterThan(0);
        RuleFor(x => x.Topic).NotEmpty().MaximumLength(200);
    }
}

public sealed class UpdateWorkTaskRequestValidator : AbstractValidator<UpdateWorkTaskRequest>
{
    public UpdateWorkTaskRequestValidator()
    {
        RuleFor(x => x.Topic).NotEmpty().MaximumLength(200);
    }
}

public sealed class AttachPredictionRequestValidator : AbstractValidator<AttachPredictionRequest>
{
    public AttachPredictionRequestValidator()
    {
        RuleFor(x => x.PredictionId).NotEmpty();
    }
}

public sealed class CreateTaskAssignmentRequestValidator : AbstractValidator<CreateTaskAssignmentRequest>
{
    public CreateTaskAssignmentRequestValidator()
    {
        RuleFor(x => x.EngineerId).NotEmpty();
    }
}

public sealed class CreateTaskReportRequestValidator : AbstractValidator<CreateTaskReportRequest>
{
    public CreateTaskReportRequestValidator()
    {
        RuleFor(x => x.ResultCode).NotEmpty().MaximumLength(50);
    }
}

public sealed class CreateTaskReturnRequestValidator : AbstractValidator<CreateTaskReturnRequest>
{
    public CreateTaskReturnRequestValidator()
    {
        RuleFor(x => x.TargetType).NotEmpty().Must(t => t is "dispatcher" or "queue" or "incident")
            .WithMessage("targetType must be 'dispatcher', 'queue' or 'incident'.");
    }
}

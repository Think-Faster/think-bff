using BFF.Contracts.Permissions;
using FluentValidation;

namespace BFF.Application.Validators;

public sealed class CreateResourceRequestValidator : AbstractValidator<CreateResourceRequest>
{
    public CreateResourceRequestValidator()
    {
        RuleFor(x => x.Code).NotEmpty().MaximumLength(100).Matches("^[a-z0-9_-]+$");
        RuleFor(x => x.Name).NotEmpty().MaximumLength(200);
    }
}

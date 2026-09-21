using BFF.Contracts.Groups;
using FluentValidation;

namespace BFF.Application.Validators;

public sealed class CreateGroupRequestValidator : AbstractValidator<CreateGroupRequest>
{
    public CreateGroupRequestValidator()
    {
        RuleFor(x => x.Code).NotEmpty().MaximumLength(100).Matches("^[a-z0-9_-]+$");
        RuleFor(x => x.Name).NotEmpty().MaximumLength(200);
    }
}

using BFF.Contracts.Permissions;
using BFF.Models.Enums;
using FluentValidation;

namespace BFF.Application.Validators;

public sealed class CreateGrantRequestValidator : AbstractValidator<CreateGrantRequest>
{
    public CreateGrantRequestValidator()
    {
        RuleFor(x => x.PrincipalType)
            .Must(value => PrincipalTypeExtensions.TryParse(value, out _))
            .WithMessage("principalType must be 'user' or 'group'.");
        RuleFor(x => x.PrincipalId).NotEmpty();
        RuleFor(x => x.ResourceCode).NotEmpty().MaximumLength(100);
        RuleFor(x => x.Permissions).NotEmpty();
        RuleForEach(x => x.Permissions)
            .Must(value => PermissionFlagsExtensions.TryParseName(value, out _))
            .WithMessage("Unknown permission name.");
    }
}

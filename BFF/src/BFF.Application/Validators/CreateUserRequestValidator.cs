using BFF.Contracts.Users;
using FluentValidation;

namespace BFF.Application.Validators;

public sealed class CreateUserRequestValidator : AbstractValidator<CreateUserRequest>
{
    public CreateUserRequestValidator()
    {
        RuleFor(x => x.AuthUserId).NotEmpty().MaximumLength(256);
        RuleFor(x => x.LastName).NotEmpty().MaximumLength(200);
        RuleFor(x => x.FirstName).NotEmpty().MaximumLength(200);
        RuleFor(x => x.MiddleName).MaximumLength(200);
        RuleForEach(x => x.GroupIds).NotEmpty().When(x => x.GroupIds is not null);
    }
}

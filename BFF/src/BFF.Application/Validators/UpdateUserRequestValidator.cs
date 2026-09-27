using BFF.Contracts.Users;
using FluentValidation;

namespace BFF.Application.Validators;

public sealed class UpdateUserRequestValidator : AbstractValidator<UpdateUserRequest>
{
    public UpdateUserRequestValidator()
    {
        RuleFor(x => x.LastName).NotEmpty().MaximumLength(200);
        RuleFor(x => x.FirstName).NotEmpty().MaximumLength(200);
        RuleFor(x => x.MiddleName).MaximumLength(200);
    }
}

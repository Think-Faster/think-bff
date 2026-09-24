using BFF.Contracts.Users;
using FluentValidation;

namespace BFF.Application.Validators;

public sealed class AssignObjectRequestValidator : AbstractValidator<AssignObjectRequest>
{
    public AssignObjectRequestValidator()
    {
        RuleFor(x => x.ObjectId).GreaterThan(0);
    }
}

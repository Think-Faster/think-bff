using BFF.Contracts.Users;
using BFF.Models.Constants;
using FluentValidation;

namespace BFF.Application.Validators;

public sealed class AssignObjectRequestValidator : AbstractValidator<AssignObjectRequest>
{
    public AssignObjectRequestValidator()
    {
        RuleFor(x => x.ObjectId).GreaterThan(0);
    }
}

public sealed class UpdateMyTelegramRequestValidator : AbstractValidator<UpdateMyTelegramRequest>
{
    public UpdateMyTelegramRequestValidator()
    {
        RuleFor(x => x.Username).Must(TelegramUsername.IsValidOrEmpty)
            .WithMessage("username must be a Telegram username: 5-32 Latin letters, digits or _, starting with a letter.");
    }
}

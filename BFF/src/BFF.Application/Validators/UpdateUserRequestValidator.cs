using BFF.Contracts.Users;
using BFF.Models.Constants;
using FluentValidation;

namespace BFF.Application.Validators;

public sealed class UpdateUserRequestValidator : AbstractValidator<UpdateUserRequest>
{
    public UpdateUserRequestValidator()
    {
        RuleFor(x => x.LastName).NotEmpty().MaximumLength(200);
        RuleFor(x => x.FirstName).NotEmpty().MaximumLength(200);
        RuleFor(x => x.MiddleName).MaximumLength(200);
        RuleFor(x => x.Email).EmailAddress().MaximumLength(320).When(x => !string.IsNullOrEmpty(x.Email));
        RuleFor(x => x.Telegram).Must(TelegramUsername.IsValidOrEmpty)
            .WithMessage("telegram must be a Telegram username: 5-32 Latin letters, digits or _, starting with a letter.");
    }
}

using BFF.Contracts.Notifications;
using FluentValidation;

namespace BFF.Application.Validators;

public sealed class SendTelegramRequestValidator : AbstractValidator<SendTelegramRequest>
{
    public SendTelegramRequestValidator()
    {
        RuleFor(x => x.Subject).NotEmpty();
        RuleFor(x => x.Text).NotEmpty();
        RuleFor(x => x.UserIds).NotEmpty().WithMessage("At least one recipient is required in userIds.");
        RuleForEach(x => x.UserIds).NotEmpty();
    }
}

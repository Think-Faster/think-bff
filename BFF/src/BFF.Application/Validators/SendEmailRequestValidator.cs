using BFF.Contracts.Notifications;
using FluentValidation;

namespace BFF.Application.Validators;

public sealed class SendEmailRequestValidator : AbstractValidator<SendEmailRequest>
{
    public SendEmailRequestValidator()
    {
        RuleFor(x => x.Subject).NotEmpty();
        RuleFor(x => x.Text).NotEmpty();
        RuleForEach(x => x.UserIds).NotEmpty().When(x => x.UserIds is not null);
        RuleForEach(x => x.Emails).NotEmpty().EmailAddress().When(x => x.Emails is not null);
        RuleFor(x => x)
            .Must(x => (x.UserIds?.Count ?? 0) + (x.Emails?.Count ?? 0) > 0)
            .WithMessage("At least one recipient is required in userIds or emails.");
    }
}

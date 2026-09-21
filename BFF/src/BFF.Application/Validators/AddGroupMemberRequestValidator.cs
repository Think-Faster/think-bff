using BFF.Contracts.Groups;
using BFF.Models.Enums;
using FluentValidation;

namespace BFF.Application.Validators;

public sealed class AddGroupMemberRequestValidator : AbstractValidator<AddGroupMemberRequest>
{
    public AddGroupMemberRequestValidator()
    {
        RuleFor(x => x.MemberId).NotEmpty();
        RuleFor(x => x.MemberType)
            .Must(value => MemberTypeExtensions.TryParse(value, out _))
            .WithMessage("memberType must be 'user' or 'group'.");
    }
}

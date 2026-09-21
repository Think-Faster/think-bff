using BFF.Contracts.Groups;
using FluentValidation;

namespace BFF.Application.Validators;

public sealed class AddGroupMembersBatchRequestValidator : AbstractValidator<AddGroupMembersBatchRequest>
{
    public AddGroupMembersBatchRequestValidator()
    {
        RuleFor(x => x.Members).NotEmpty();
        RuleForEach(x => x.Members).SetValidator(new AddGroupMemberRequestValidator());
    }
}

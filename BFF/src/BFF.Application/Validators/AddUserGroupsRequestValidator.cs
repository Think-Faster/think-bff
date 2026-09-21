using BFF.Contracts.Users;
using FluentValidation;

namespace BFF.Application.Validators;

public sealed class AddUserGroupsRequestValidator : AbstractValidator<AddUserGroupsRequest>
{
    public AddUserGroupsRequestValidator()
    {
        RuleFor(x => x.GroupIds).NotEmpty();
        RuleForEach(x => x.GroupIds).NotEmpty();
    }
}

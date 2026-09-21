namespace BFF.Contracts.Users;

public sealed class AddUserGroupsRequest
{
    public IReadOnlyList<Guid> GroupIds { get; init; } = Array.Empty<Guid>();
}

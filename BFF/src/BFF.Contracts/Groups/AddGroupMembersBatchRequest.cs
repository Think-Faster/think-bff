namespace BFF.Contracts.Groups;

public sealed class AddGroupMembersBatchRequest
{
    public IReadOnlyList<AddGroupMemberRequest> Members { get; init; } = Array.Empty<AddGroupMemberRequest>();
}

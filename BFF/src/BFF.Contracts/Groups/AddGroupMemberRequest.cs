namespace BFF.Contracts.Groups;

public sealed class AddGroupMemberRequest
{
    public Guid MemberId { get; init; }
    public string MemberType { get; init; } = string.Empty; // "user" | "group"
}

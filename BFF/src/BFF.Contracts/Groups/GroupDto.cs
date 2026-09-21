namespace BFF.Contracts.Groups;

public sealed class GroupDto
{
    public Guid Id { get; init; }
    public string Code { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public bool IsSystem { get; init; }
    public IReadOnlyList<GroupMemberDto> Members { get; init; } = Array.Empty<GroupMemberDto>();
}

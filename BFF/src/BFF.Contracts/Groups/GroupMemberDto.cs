namespace BFF.Contracts.Groups;

/// <summary>First-level group member: either a user or a nested group, distinguished by Type.</summary>
public sealed class GroupMemberDto
{
    public string Type { get; init; } = string.Empty; // "user" | "group"
    public Guid Id { get; init; }
    public string DisplayName { get; init; } = string.Empty;
    public string? Code { get; init; } // present only when Type == "group"
}

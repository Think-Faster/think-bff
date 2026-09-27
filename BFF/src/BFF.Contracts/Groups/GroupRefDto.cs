namespace BFF.Contracts.Groups;

/// <summary>First-level group reference, used when listing a user's direct group memberships.</summary>
public sealed class GroupRefDto
{
    public Guid Id { get; init; }
    public string Code { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
}

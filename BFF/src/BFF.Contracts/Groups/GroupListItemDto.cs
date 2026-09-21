namespace BFF.Contracts.Groups;

public sealed class GroupListItemDto
{
    public Guid Id { get; init; }
    public string Code { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public bool IsSystem { get; init; }
}

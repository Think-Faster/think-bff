namespace BFF.Contracts.Permissions;

public sealed class ResourceDto
{
    public int Id { get; init; }
    public string Code { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
}

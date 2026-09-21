namespace BFF.Contracts.Groups;

public sealed class CreateGroupRequest
{
    public string Code { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
}

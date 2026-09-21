namespace BFF.Contracts.Permissions;

public sealed class CreateResourceRequest
{
    public string Code { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
}

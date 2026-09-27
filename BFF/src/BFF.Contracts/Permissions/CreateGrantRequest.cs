namespace BFF.Contracts.Permissions;

public sealed class CreateGrantRequest
{
    public string PrincipalType { get; init; } = string.Empty; // "user" | "group"
    public Guid PrincipalId { get; init; }
    public string ResourceCode { get; init; } = string.Empty;
    public IReadOnlyList<string> Permissions { get; init; } = Array.Empty<string>();
}

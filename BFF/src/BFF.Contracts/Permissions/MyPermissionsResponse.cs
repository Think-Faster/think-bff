namespace BFF.Contracts.Permissions;

public sealed class MyPermissionsResponse
{
    public Guid UserId { get; init; }

    /// <summary>Resource code -> list of permission names, e.g. "documents" -> ["create", "read"].</summary>
    public IReadOnlyDictionary<string, IReadOnlyList<string>> Permissions { get; init; }
        = new Dictionary<string, IReadOnlyList<string>>();
}

namespace BFF.Contracts.Permissions;

public sealed class MyPermissionsResponse
{
    public Guid UserId { get; init; }

    /// <summary>Resource code -> list of permission names, e.g. "documents" -> ["create", "read"].</summary>
    public IReadOnlyDictionary<string, IReadOnlyList<string>> Permissions { get; init; }
        = new Dictionary<string, IReadOnlyList<string>>();

    /// <summary>Codes of the user's groups, direct and ancestor ones (e.g. a brigade inside "engineers" gives
    /// both). Roles are groups: the UI picks the engineer section by "engineers", not by a permission —
    /// admins hold every permission but are not engineers.</summary>
    public IReadOnlyList<string> Groups { get; init; } = Array.Empty<string>();
}

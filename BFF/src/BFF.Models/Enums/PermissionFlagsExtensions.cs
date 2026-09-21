namespace BFF.Models.Enums;

public static class PermissionFlagsExtensions
{
    private static readonly (PermissionFlags Flag, string Name)[] Map =
    {
        (PermissionFlags.Create, "create"),
        (PermissionFlags.Read, "read"),
        (PermissionFlags.Update, "update"),
        (PermissionFlags.Delete, "delete"),
        (PermissionFlags.Export, "export"),
        (PermissionFlags.Import, "import"),
        (PermissionFlags.Manage, "manage"),
    };

    /// <summary>Bitmask -> lowercase permission names, for JSON responses (section 10.3: "в JSON маску
    /// отдавать массивом строк, не числом").</summary>
    public static IReadOnlyList<string> ToNames(this PermissionFlags mask) =>
        Map.Where(m => mask.HasFlag(m.Flag)).Select(m => m.Name).ToArray();

    /// <summary>Lowercase permission names -> bitmask. Throws <see cref="ArgumentException"/> on an
    /// unrecognized name.</summary>
    public static PermissionFlags ParseNames(IEnumerable<string> names)
    {
        var result = PermissionFlags.None;
        foreach (var name in names)
        {
            if (!TryParseName(name, out var flag))
            {
                throw new ArgumentException($"Unknown permission name: {name}", nameof(names));
            }

            result |= flag;
        }

        return result;
    }

    public static bool TryParseName(string name, out PermissionFlags flag)
    {
        foreach (var (mappedFlag, mappedName) in Map)
        {
            if (string.Equals(mappedName, name, StringComparison.OrdinalIgnoreCase))
            {
                flag = mappedFlag;
                return true;
            }
        }

        flag = PermissionFlags.None;
        return false;
    }
}

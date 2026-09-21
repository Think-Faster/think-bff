namespace BFF.Models.Enums;

public static class PrincipalTypeExtensions
{
    public static string ToApiString(this PrincipalType type) => type switch
    {
        PrincipalType.User => "user",
        PrincipalType.Group => "group",
        _ => throw new ArgumentOutOfRangeException(nameof(type), type, null)
    };

    public static bool TryParse(string? value, out PrincipalType type)
    {
        switch (value?.Trim().ToLowerInvariant())
        {
            case "user":
                type = PrincipalType.User;
                return true;
            case "group":
                type = PrincipalType.Group;
                return true;
            default:
                type = default;
                return false;
        }
    }
}

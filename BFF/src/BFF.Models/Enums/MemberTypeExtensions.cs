namespace BFF.Models.Enums;

public static class MemberTypeExtensions
{
    public static string ToApiString(this MemberType type) => type switch
    {
        MemberType.User => "user",
        MemberType.Group => "group",
        _ => throw new ArgumentOutOfRangeException(nameof(type), type, null)
    };

    public static bool TryParse(string? value, out MemberType type)
    {
        switch (value?.Trim().ToLowerInvariant())
        {
            case "user":
                type = MemberType.User;
                return true;
            case "group":
                type = MemberType.Group;
                return true;
            default:
                type = default;
                return false;
        }
    }
}

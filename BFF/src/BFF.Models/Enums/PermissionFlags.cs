namespace BFF.Models.Enums;

[Flags]
public enum PermissionFlags
{
    None = 0,
    Create = 1 << 0,
    Read = 1 << 1,
    Update = 1 << 2,
    Delete = 1 << 3,
    Export = 1 << 4,
    Import = 1 << 5,
    Manage = 1 << 6,
    All = Create | Read | Update | Delete | Export | Import | Manage
}

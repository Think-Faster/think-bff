namespace BFF.Application.Services;

public sealed class PermissionCacheOptions
{
    public const string SectionName = "PermissionCache";

    public int TtlSeconds { get; set; } = 300;
    public int VersionPollSeconds { get; set; } = 5;
}

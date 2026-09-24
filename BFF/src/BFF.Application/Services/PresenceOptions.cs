namespace BFF.Application.Services;

/// <summary>Section 6.4 of the source domain doc explicitly says "5 minutes is a working value, make it
/// a setting, not a hardcoded constant".</summary>
public sealed class PresenceOptions
{
    public const string SectionName = "Presence";

    public int OnlineWindowSeconds { get; set; } = 300;
    public int FlushIntervalSeconds { get; set; } = 30;
}

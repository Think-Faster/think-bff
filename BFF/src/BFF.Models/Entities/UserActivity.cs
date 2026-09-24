namespace BFF.Models.Entities;

/// <summary>Presence: one row per user, overwritten on every authenticated request (throttled — see
/// IPresenceTracker). No tokens/sessions are stored anywhere; this is the only trace of "online".</summary>
public sealed class UserActivity
{
    public Guid UserId { get; set; }
    public DateTimeOffset LastSeenAt { get; set; }
    public string? LastAction { get; set; }
}

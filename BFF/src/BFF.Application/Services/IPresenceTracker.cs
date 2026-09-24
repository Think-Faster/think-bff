using BFF.Contracts.Users;

namespace BFF.Application.Services;

public interface IPresenceTracker
{
    /// <summary>Called from TokenAuthenticationMiddleware on every authenticated request. Throttled
    /// internally (PresenceOptions.FlushIntervalSeconds) — most calls are a cache hit and never touch
    /// the database (section 6.4 of the source domain doc).</summary>
    Task TrackAsync(Guid userId, string? action, CancellationToken ct);

    Task<IReadOnlyList<PresenceDto>> ListAsync(IReadOnlyCollection<Guid>? userIds, CancellationToken ct);
}

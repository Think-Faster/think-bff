using BFF.Context;
using BFF.Contracts.Users;
using BFF.Models.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;

namespace BFF.Application.Services;

public sealed class PresenceTracker : IPresenceTracker
{
    private const string CacheKeyPrefix = "presence:flushed:";

    private readonly BffDbContext _context;
    private readonly IMemoryCache _cache;
    private readonly PresenceOptions _options;

    public PresenceTracker(BffDbContext context, IMemoryCache cache, IOptions<PresenceOptions> options)
    {
        _context = context;
        _cache = cache;
        _options = options.Value;
    }

    public async Task TrackAsync(Guid userId, string? action, CancellationToken ct)
    {
        var cacheKey = CacheKeyPrefix + userId;
        if (_cache.TryGetValue(cacheKey, out _))
        {
            // Flushed within the last FlushIntervalSeconds — skip the DB round trip.
            return;
        }

        var now = DateTimeOffset.UtcNow;
        var existing = await _context.UserActivities.SingleOrDefaultAsync(a => a.UserId == userId, ct);

        if (existing is null)
        {
            _context.UserActivities.Add(new UserActivity { UserId = userId, LastSeenAt = now, LastAction = action });
        }
        else
        {
            existing.LastSeenAt = now;
            existing.LastAction = action;
        }

        await _context.SaveChangesAsync(ct);
        _cache.Set(cacheKey, true, TimeSpan.FromSeconds(_options.FlushIntervalSeconds));
    }

    public async Task<IReadOnlyList<PresenceDto>> ListAsync(IReadOnlyCollection<Guid>? userIds, CancellationToken ct)
    {
        var query = _context.UserActivities.AsNoTracking().AsQueryable();
        if (userIds is { Count: > 0 })
        {
            query = query.Where(a => userIds.Contains(a.UserId));
        }

        var rows = await query.ToListAsync(ct);
        var cutoff = DateTimeOffset.UtcNow.AddSeconds(-_options.OnlineWindowSeconds);

        return rows.Select(a => new PresenceDto
        {
            UserId = a.UserId,
            IsOnline = a.LastSeenAt >= cutoff,
            LastSeenAt = a.LastSeenAt,
            LastAction = a.LastAction,
        }).ToList();
    }
}

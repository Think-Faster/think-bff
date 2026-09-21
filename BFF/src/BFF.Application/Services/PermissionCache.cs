using BFF.Context.Repositories;
using BFF.Models.Enums;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;

namespace BFF.Application.Services;

public sealed class PermissionCache : IPermissionCache
{
    private const string VersionCacheKey = "rbac:version";

    private readonly IMemoryCache _cache;
    private readonly IPermissionRepository _repository;
    private readonly PermissionCacheOptions _options;

    public PermissionCache(IMemoryCache cache, IPermissionRepository repository, IOptions<PermissionCacheOptions> options)
    {
        _cache = cache;
        _repository = repository;
        _options = options.Value;
    }

    public async Task<IReadOnlyDictionary<string, PermissionFlags>> GetPermissionsAsync(Guid userId, CancellationToken ct)
    {
        var version = await GetVersionAsync(ct);
        var key = $"perm:{userId}:{version}";

        if (_cache.TryGetValue(key, out IReadOnlyDictionary<string, PermissionFlags>? cached) && cached is not null)
        {
            return cached;
        }

        var raw = await _repository.GetEffectivePermissionsAsync(userId, ct);
        var permissions = (IReadOnlyDictionary<string, PermissionFlags>)raw.ToDictionary(
            kv => kv.Key, kv => (PermissionFlags)kv.Value);

        _cache.Set(key, permissions, TimeSpan.FromSeconds(_options.TtlSeconds));
        return permissions;
    }

    private async Task<long> GetVersionAsync(CancellationToken ct)
    {
        if (_cache.TryGetValue(VersionCacheKey, out long version))
        {
            return version;
        }

        version = await _repository.GetRbacVersionAsync(ct);
        _cache.Set(VersionCacheKey, version, TimeSpan.FromSeconds(_options.VersionPollSeconds));
        return version;
    }
}

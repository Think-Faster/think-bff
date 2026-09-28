using StackExchange.Redis;

namespace BFF.WebApi.Notifications;

/// <summary>Не даёт слать письмо на один и тот же адрес чаще раза в минуту — SET NX EX в Redis, атомарно
/// (первый, кто успел записать ключ, получает право отправить; остальные до истечения TTL — нет).
/// Без REDIS_HOST лимитер отключён (как и AuditWriter — тот же принцип "не роняем функциональность из-за
/// отсутствия Redis в локальной разработке", см. docs/DECISIONS.md), при ошибке связи с Redis —
/// fail-open (разрешить отправку), см. ту же запись.</summary>
public sealed class EmailRateLimiter : IDisposable
{
    private static readonly TimeSpan Window = TimeSpan.FromMinutes(1);
    private const string KeyPrefix = "email:ratelimit:";

    private readonly Lazy<ConnectionMultiplexer>? _redis;
    private readonly ILogger<EmailRateLimiter> _logger;

    public EmailRateLimiter(IConfiguration configuration, ILogger<EmailRateLimiter> logger)
    {
        _logger = logger;
        var host = configuration["REDIS_HOST"];
        if (string.IsNullOrEmpty(host))
        {
            return;
        }

        var options = new ConfigurationOptions
        {
            Password = configuration["TF_REDIS_PASSWORD"],
            AbortOnConnectFail = false,
            ConnectTimeout = 2000,
            SyncTimeout = 2000,
            AsyncTimeout = 2000,
        };
        options.EndPoints.Add(host);
        _redis = new Lazy<ConnectionMultiplexer>(() => ConnectionMultiplexer.Connect(options));
    }

    /// <returns>true — отправка разрешена и уже зафиксирована в Redis на ближайшую минуту.</returns>
    public async Task<bool> TryAcquireAsync(string email, CancellationToken ct)
    {
        if (_redis is null)
        {
            return true;
        }

        try
        {
            return await _redis.Value.GetDatabase()
                .StringSetAsync(KeyPrefix + email.ToLowerInvariant(), "1", Window, When.NotExists);
        }
        catch (Exception ex)
        {
            _logger.LogWarning("Email rate limiter unavailable ({Error}) — allowing send to {Email}", ex.GetType().Name, email);
            return true;
        }
    }

    public void Dispose()
    {
        if (_redis is { IsValueCreated: true })
        {
            _redis.Value.Dispose();
        }
    }
}

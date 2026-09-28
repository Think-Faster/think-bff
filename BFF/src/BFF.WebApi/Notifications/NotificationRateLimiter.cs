using StackExchange.Redis;

namespace BFF.WebApi.Notifications;

/// <summary>Не даёт слать человеку уведомление чаще раза в минуту по каждому каналу — письмо на один и тот
/// же адрес, сообщение в Telegram на одно и то же имя. SET NX EX в Redis, атомарно (первый, кто успел
/// записать ключ, получает право отправить; остальные до истечения TTL — нет). Без REDIS_HOST лимитер
/// отключён (как и AuditWriter — тот же принцип "не роняем функциональность из-за отсутствия Redis в
/// локальной разработке", см. docs/DECISIONS.md), при ошибке связи с Redis — fail-open (разрешить
/// отправку), см. ту же запись.</summary>
public sealed class NotificationRateLimiter : IDisposable
{
    private static readonly TimeSpan Window = TimeSpan.FromMinutes(1);
    private const string EmailKeyPrefix = "email:ratelimit:";
    private const string TelegramKeyPrefix = "telegram:ratelimit:";

    private readonly Lazy<ConnectionMultiplexer>? _redis;
    private readonly ILogger<NotificationRateLimiter> _logger;

    public NotificationRateLimiter(IConfiguration configuration, ILogger<NotificationRateLimiter> logger)
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
    public Task<bool> TryAcquireEmailAsync(string email, CancellationToken ct) =>
        TryAcquireAsync(EmailKeyPrefix + email.ToLowerInvariant());

    /// <param name="username">Имя в Telegram, уже нормализованное (TelegramUsername).</param>
    public Task<bool> TryAcquireTelegramAsync(string username, CancellationToken ct) =>
        TryAcquireAsync(TelegramKeyPrefix + username);

    private async Task<bool> TryAcquireAsync(string key)
    {
        if (_redis is null)
        {
            return true;
        }

        try
        {
            return await _redis.Value.GetDatabase().StringSetAsync(key, "1", Window, When.NotExists);
        }
        catch (Exception ex)
        {
            _logger.LogWarning("Notification rate limiter unavailable ({Error}) — allowing send ({Key})", ex.GetType().Name, key);
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

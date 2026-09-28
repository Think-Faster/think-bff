using StackExchange.Redis;

namespace BFF.WebApi.Notifications;

/// <summary>Linked null — неизвестно (Redis недоступен или не настроен); Bot — имя бота без @.</summary>
public sealed record TelegramLink(bool? Linked, string? Bot);

/// <summary>
/// Кто подключил бота — только чтение ключей tf-tg в общем Redis (think-infra/telegram/app/links.py):
/// `tg:user:&lt;username&gt;` = chat_id (человек нажал «Старт» у бота) и `tg:bot` — имя бота. Пишет их
/// только tf-tg; BFF по ним показывает статус в профиле и не ставит в очередь сообщение тем, кто бота не
/// подключил. Без REDIS_HOST или при недоступном Redis — «неизвестно», отправка не блокируется: tf-tg
/// всё равно проверит связь сам при доставке.
/// </summary>
public sealed class TelegramLinks : IDisposable
{
    private const string UserKeyPrefix = "tg:user:";
    private const string BotKey = "tg:bot";

    private readonly Lazy<ConnectionMultiplexer>? _redis;
    private readonly ILogger<TelegramLinks> _logger;

    public TelegramLinks(IConfiguration configuration, ILogger<TelegramLinks> logger)
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

    /// <param name="username">Нормализованное имя (TelegramUsername) или null — тогда Linked=false.</param>
    public async Task<TelegramLink> GetAsync(string? username, CancellationToken ct)
    {
        if (_redis is null)
        {
            return new TelegramLink(null, null);
        }

        try
        {
            var db = _redis.Value.GetDatabase();
            var bot = await db.StringGetAsync(BotKey);
            var linked = username is not null && await db.KeyExistsAsync(UserKeyPrefix + username);
            return new TelegramLink(linked, bot.IsNullOrEmpty ? null : bot.ToString());
        }
        catch (Exception ex)
        {
            _logger.LogWarning("Telegram links unavailable ({Error})", ex.GetType().Name);
            return new TelegramLink(null, null);
        }
    }

    /// <returns>Подключил ли бота каждый из <paramref name="usernames"/>; null — неизвестно.</returns>
    public async Task<IReadOnlyDictionary<string, bool>?> LinkedAsync(IReadOnlyCollection<string> usernames, CancellationToken ct)
    {
        if (_redis is null || usernames.Count == 0)
        {
            return null;
        }

        try
        {
            var db = _redis.Value.GetDatabase();
            var result = new Dictionary<string, bool>();
            foreach (var username in usernames)
            {
                result[username] = await db.KeyExistsAsync(UserKeyPrefix + username);
            }

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogWarning("Telegram links unavailable ({Error}) — sending without the check", ex.GetType().Name);
            return null;
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

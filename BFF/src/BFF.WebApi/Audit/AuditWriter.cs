using System.Text.Json;
using System.Text.Json.Serialization;
using StackExchange.Redis;

namespace BFF.WebApi.Audit;

/// <summary>Одна строка журнала действий — формат docs/common/права-и-аудит.md §6.4 и §6.7.</summary>
public sealed record AuditEvent(
    string EventType,
    string Outcome,
    string ActorKind,
    string? ActorId,
    string? ActorLogin,
    string? RequestId,
    string? Ip,
    string? ObjectType,
    string? ObjectId,
    IReadOnlyDictionary<string, object?> Details)
{
    public string EventId { get; init; } = Guid.NewGuid().ToString();
    public DateTimeOffset OccurredAt { get; init; } = DateTimeOffset.UtcNow;
    public string Service { get; init; } = "bff";
    public int? AreaId { get; init; }
}

/// <summary>
/// Кладёт событие в поток Redis `audit` (XADD audit * event &lt;json&gt;) — вариант Б §6.5; в базу его
/// пишет сервис аудита. Аудит не роняет запрос: без REDIS_HOST или при недоступном Redis событие
/// уходит в лог сервиса.
/// </summary>
public sealed class AuditWriter : IDisposable
{
    private const string Stream = "audit";
    private const int MaxLength = 1_000_000;

    private static readonly JsonSerializerOptions Json = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        DefaultIgnoreCondition = JsonIgnoreCondition.Never,
    };

    private readonly Lazy<ConnectionMultiplexer>? _redis;
    private readonly ILogger<AuditWriter> _logger;

    public AuditWriter(IConfiguration configuration, ILogger<AuditWriter> logger)
    {
        _logger = logger;
        var host = configuration["REDIS_HOST"];
        if (string.IsNullOrEmpty(host))
        {
            return;
        }

        var options = new ConfigurationOptions
        {
            // Пароль приходит из Vault (secret/tf/redis) переменной TF_REDIS_PASSWORD — vault-entrypoint.sh.
            Password = configuration["TF_REDIS_PASSWORD"],
            AbortOnConnectFail = false,
            ConnectTimeout = 2000,
            SyncTimeout = 2000,
            AsyncTimeout = 2000,
        };
        options.EndPoints.Add(host);
        _redis = new Lazy<ConnectionMultiplexer>(() => ConnectionMultiplexer.Connect(options));
    }

    public async Task WriteAsync(AuditEvent auditEvent)
    {
        var payload = JsonSerializer.Serialize(auditEvent, Json);
        if (_redis is null)
        {
            _logger.LogInformation("Audit without transport: {AuditEvent}", payload);
            return;
        }

        try
        {
            await _redis.Value.GetDatabase().StreamAddAsync(
                Stream, "event", payload, maxLength: MaxLength, useApproximateMaxLength: true);
        }
        catch (Exception ex)
        {
            _logger.LogWarning("Audit stream unavailable ({Error}): {AuditEvent}", ex.GetType().Name, payload);
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

using System.Text.Json;
using BFF.Application.Services;
using BFF.Contracts.Predictions;
using BFF.Models.Enums;
using Confluent.Kafka;

namespace BFF.WebApi.Notifications;

/// <summary>
/// Канал «по факту» модели (ML/INTEGRATION.md §2.3, §13.7): в tf.forecast.results приходит сообщение с
/// kind "fact", у каждого живого типа — started_at, last_at и new. new=true — объявление: BFF заводит
/// FactAlert и рассылает уведомление (FactNotifier); new=false — обновление того же эпизода, повторно не
/// шлём. clock "replay" — демонстрационное время модели: по нему ни записей, ни уведомлений.
///
/// Прогнозы (kind "forecast") этот потребитель пропускает. Группа — tf-bff-facts (ACL think-infra: группы
/// с префиксом tf-bff). Без пароля Kafka (TF_KAFKA_BFF_PASSWORD из Vault, путь kafka/bff) не стартует —
/// остальной BFF работает как раньше.
/// </summary>
public sealed class FactResultsConsumer : BackgroundService
{
    private const string Topic = "tf.forecast.results";

    private static readonly Dictionary<string, PredictionType> Types = new(StringComparer.OrdinalIgnoreCase)
    {
        ["fire"] = PredictionType.Fire,
        ["gas"] = PredictionType.Gas,
        ["flood"] = PredictionType.Flood,
        ["equipment"] = PredictionType.EquipmentFailure,
        ["sensor"] = PredictionType.SensorFailure,
        ["intrusion"] = PredictionType.Intrusion,
    };

    private readonly IServiceScopeFactory _scopes;
    private readonly IConfiguration _configuration;
    private readonly ILogger<FactResultsConsumer> _logger;

    public FactResultsConsumer(IServiceScopeFactory scopes, IConfiguration configuration, ILogger<FactResultsConsumer> logger)
    {
        _scopes = scopes;
        _configuration = configuration;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var password = _configuration["TF_KAFKA_BFF_PASSWORD"];
        if (string.IsNullOrEmpty(password))
        {
            _logger.LogWarning("TF_KAFKA_BFF_PASSWORD is not set — fact alerts from {Topic} are not consumed", Topic);
            return;
        }

        var config = new ConsumerConfig
        {
            BootstrapServers = _configuration["KAFKA_BOOTSTRAP"] ?? "tf-kafka:9092",
            SecurityProtocol = SecurityProtocol.SaslPlaintext,
            SaslMechanism = SaslMechanism.Plain,
            SaslUsername = _configuration["KAFKA_USER"] ?? "tf-bff",
            SaslPassword = password,
            GroupId = _configuration["KAFKA_FACTS_GROUP"] ?? "tf-bff-facts",
            // Первый запуск группы начинает с конца: старые факты не должны разослаться пачкой.
            AutoOffsetReset = AutoOffsetReset.Latest,
            EnableAutoCommit = false,
            AllowAutoCreateTopics = false,
        };

        // Consume блокирует поток — свой поток, чтобы не держать старт приложения.
        await Task.Factory.StartNew(() => Run(config, stoppingToken), stoppingToken,
            TaskCreationOptions.LongRunning, TaskScheduler.Default).Unwrap();
    }

    private async Task Run(ConsumerConfig config, CancellationToken ct)
    {
        using var consumer = new ConsumerBuilder<string, string>(config)
            .SetErrorHandler((_, e) => _logger.LogWarning("Kafka: {Reason}", e.Reason))
            .Build();
        consumer.Subscribe(Topic);
        _logger.LogInformation("Consuming fact alerts from {Topic} as {Group}", Topic, config.GroupId);

        try
        {
            while (!ct.IsCancellationRequested)
            {
                ConsumeResult<string, string>? result;
                try
                {
                    result = consumer.Consume(ct);
                }
                catch (ConsumeException ex)
                {
                    _logger.LogWarning("Kafka consume failed: {Reason}", ex.Error.Reason);
                    await Task.Delay(TimeSpan.FromSeconds(5), ct);
                    continue;
                }

                if (result?.Message?.Value is not { } value)
                {
                    continue;
                }

                await HandleWithRetryAsync(value, ct);
                consumer.Commit(result);
            }
        }
        catch (OperationCanceledException)
        {
        }
        finally
        {
            consumer.Close();
        }
    }

    // База или брокер уведомлений могут лежать — несколько попыток, потом сообщение пропускается с записью
    // в лог: вечный повтор одного сообщения остановил бы все следующие.
    private async Task HandleWithRetryAsync(string value, CancellationToken ct)
    {
        for (var attempt = 1; ; attempt++)
        {
            try
            {
                await HandleAsync(value, ct);
                return;
            }
            catch (Exception ex) when (ex is not OperationCanceledException && attempt < 4)
            {
                _logger.LogWarning(ex, "Fact message failed, attempt {Attempt}", attempt);
                await Task.Delay(TimeSpan.FromSeconds(5 * attempt), ct);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogError(ex, "Fact message dropped after {Attempt} attempts: {Message}", attempt, value);
                return;
            }
        }
    }

    public async Task HandleAsync(string value, CancellationToken ct)
    {
        foreach (var fact in Parse(value, _logger))
        {
            using var scope = _scopes.CreateScope();
            var predictions = scope.ServiceProvider.GetRequiredService<IPredictionService>();
            var (alert, created) = await predictions.RecordFactAlertAsync(fact.Request, ct);
            if (!created)
            {
                continue;
            }

            var notifier = scope.ServiceProvider.GetRequiredService<FactNotifier>();
            await notifier.NotifyAsync(alert, fact.Context, ct);
        }
    }

    public sealed record Fact(CreateFactAlertRequest Request, FactContext Context);

    /// <summary>Объявления (new=true) из сообщения модели; прогнозы, replay и обновления — пусто.</summary>
    public static IReadOnlyList<Fact> Parse(string value, ILogger? logger = null)
    {
        JsonDocument doc;
        try
        {
            doc = JsonDocument.Parse(value);
        }
        catch (JsonException)
        {
            logger?.LogWarning("Not a JSON message in {Topic}", Topic);
            return Array.Empty<Fact>();
        }

        using (doc)
        {
            var root = doc.RootElement;
            if (root.ValueKind != JsonValueKind.Object
                || Text(root, "kind") != "fact"
                || (Text(root, "clock") ?? "live") != "live"
                || !root.TryGetProperty("object_id", out var objectEl) || !objectEl.TryGetInt32(out var objectId)
                || !root.TryGetProperty("types", out var types) || types.ValueKind != JsonValueKind.Object)
            {
                return Array.Empty<Fact>();
            }

            var announced = DateTimeOffset.UtcNow;
            var facts = new List<Fact>();
            foreach (var entry in types.EnumerateObject())
            {
                var block = entry.Value;
                if (block.ValueKind != JsonValueKind.Object
                    || !block.TryGetProperty("new", out var isNew) || isNew.ValueKind != JsonValueKind.True
                    || !Types.TryGetValue(entry.Name, out var type)
                    || !DateTimeOffset.TryParse(Text(block, "started_at"), out var startedAt))
                {
                    continue;
                }

                long? workId = block.TryGetProperty("work_id", out var w) && w.TryGetInt64(out var id) ? id : null;
                facts.Add(new Fact(
                    new CreateFactAlertRequest { ObjectId = objectId, Type = type, StartedAt = startedAt, AnnouncedAt = announced },
                    new FactContext(Text(block, "note"), workId)));
            }

            return facts;
        }
    }

    private static string? Text(JsonElement element, string name) =>
        element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() : null;
}

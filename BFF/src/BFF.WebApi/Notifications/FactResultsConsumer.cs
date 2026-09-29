using System.Globalization;
using System.Text;
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
/// Прогнозы (kind "forecast", §2.3): модель шлёт каждый объект каждый час по всем типам. Типы с
/// alarm=true ведут карточку эпизода (IPredictionService.RecordForecastAsync) — её видит «Журнал
/// прогнозов»; alarm=false кончает тревогу пары (EndAlarmsAsync, §9.8): карточка без решения — «истёк». Прогнозы в демонстрационном времени (clock "replay") берутся, только если
/// TF_FORECAST_REPLAY=true: на стенде для показа, в проде — нет.
///
/// Группа — tf-bff-facts (ACL think-infra: группы с префиксом tf-bff). Без пароля Kafka
/// (TF_KAFKA_BFF_PASSWORD из Vault, путь kafka/bff) не стартует — остальной BFF работает как раньше.
/// </summary>
public sealed class FactResultsConsumer : BackgroundService
{
    private const string Topic = "tf.forecast.results";

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
        var acceptReplay = string.Equals(_configuration["TF_FORECAST_REPLAY"], "true", StringComparison.OrdinalIgnoreCase);
        if (ParseForecastMessage(value, acceptReplay) is { } message)
        {
            using var scope = _scopes.CreateScope();
            var predictions = scope.ServiceProvider.GetRequiredService<IPredictionService>();
            var ended = await predictions.EndAlarmsAsync(message.ObjectId, message.Ended, message.HourEnd, ct);
            if (ended > 0)
            {
                _logger.LogInformation("Forecast alarms ended: object {ObjectId}, {Count} card(s)", message.ObjectId, ended);
            }

            foreach (var forecast in message.Alarms)
            {
                var (prediction, created) = await predictions.RecordForecastAsync(forecast, ct);
                if (created)
                {
                    _logger.LogInformation("Forecast {PredictionId}: object {ObjectId} {Type}",
                        prediction.Id, prediction.ObjectId, prediction.Type);
                }
            }
        }

        foreach (var fact in Parse(value, _logger))
        {
            using var scope = _scopes.CreateScope();
            var predictions = scope.ServiceProvider.GetRequiredService<IPredictionService>();
            var (alert, created) = await predictions.RecordFactAlertAsync(fact.Request, fact.Announcement, ct);
            if (!created)
            {
                continue;
            }

            var notifier = scope.ServiceProvider.GetRequiredService<FactNotifier>();
            await notifier.NotifyAsync(alert, fact.Context, ct);
        }
    }

    public sealed record Fact(CreateFactAlertRequest Request, FactContext Context, bool Announcement);

    // Поля блока, которые BFF хранит подробностями эпизода (§13.11); ключи — в camelCase, как в API.
    private static readonly string[] DetailKeys =
        { "direction", "channels", "cause", "share", "possible_accident", "temperature" };

    /// <summary>Эпизоды из сообщения модели: объявления (new=true) и обновления (new=false) живых типов.
    /// Прогнозы, replay и незнакомые типы — пусто.</summary>
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
                    || !block.TryGetProperty("new", out var isNew)
                    || isNew.ValueKind is not (JsonValueKind.True or JsonValueKind.False)
                    || !PredictionTypeExtensions.TryParseModel(entry.Name, out var type)
                    || !TryTime(block, "started_at", out var startedAt))
                {
                    continue;
                }

                var route = Route(block);
                long? workId = block.TryGetProperty("work_id", out var w) && w.TryGetInt64(out var id) ? id : null;
                facts.Add(new Fact(
                    new CreateFactAlertRequest
                    {
                        ObjectId = objectId,
                        Type = type,
                        StartedAt = startedAt,
                        AnnouncedAt = announced,
                        LastAt = TryTime(block, "last_at", out var lastAt) ? lastAt : startedAt,
                        TriggerSensorIds = route.Select(p => p.SensorId).Distinct().ToArray(),
                        Route = route,
                        DetailsJson = Details(block),
                    },
                    new FactContext(Text(block, "note"), workId),
                    isNew.ValueKind == JsonValueKind.True));
            }

            return facts;
        }
    }

    /// <summary>Маршрут нарушителя (§13.11): сработки охраны по времени; повтор датчика подряд модель уже
    /// схлопнула.</summary>
    private static IReadOnlyList<FactRoutePointDto> Route(JsonElement block)
    {
        if (!block.TryGetProperty("route", out var route) || route.ValueKind != JsonValueKind.Array)
        {
            return Array.Empty<FactRoutePointDto>();
        }

        var result = new List<FactRoutePointDto>();
        foreach (var p in route.EnumerateArray())
        {
            if (p.ValueKind == JsonValueKind.Object
                && p.TryGetProperty("sensor_id", out var sid) && sid.TryGetInt32(out var sensorId)
                && TryTime(p, "at", out var at))
            {
                result.Add(new FactRoutePointDto { SensorId = sensorId, SType = Text(p, "stype"), At = at });
            }
        }

        return result;
    }

    /// <summary>Подробности типа (DetailKeys) одним JSON-объектом с ключами в camelCase.</summary>
    private static string? Details(JsonElement block)
    {
        var keys = DetailKeys.Where(k => block.TryGetProperty(k, out _)).ToList();
        if (keys.Count == 0)
        {
            return null;
        }

        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream))
        {
            writer.WriteStartObject();
            foreach (var key in keys)
            {
                writer.WritePropertyName(CamelCase(key));
                WriteCamelCase(writer, block.GetProperty(key));
            }

            writer.WriteEndObject();
        }

        return Encoding.UTF8.GetString(stream.ToArray());
    }

    private static void WriteCamelCase(Utf8JsonWriter writer, JsonElement element)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.Object:
                writer.WriteStartObject();
                foreach (var p in element.EnumerateObject())
                {
                    writer.WritePropertyName(CamelCase(p.Name));
                    WriteCamelCase(writer, p.Value);
                }

                writer.WriteEndObject();
                break;
            case JsonValueKind.Array:
                writer.WriteStartArray();
                foreach (var item in element.EnumerateArray())
                {
                    WriteCamelCase(writer, item);
                }

                writer.WriteEndArray();
                break;
            default:
                element.WriteTo(writer);
                break;
        }
    }

    // possible_accident → possibleAccident, sensor_id → sensorId.
    private static string CamelCase(string snake)
    {
        var parts = snake.Split('_', StringSplitOptions.RemoveEmptyEntries);
        return string.Concat(parts.Select((part, i) =>
            i == 0 ? part : char.ToUpperInvariant(part[0]) + part[1..]));
    }

    // Модель пишет время с поясом (+03:00), а Npgsql кладёт в timestamptz только UTC — приводим сразу.
    private static bool TryTime(JsonElement element, string name, out DateTimeOffset value) =>
        DateTimeOffset.TryParse(Text(element, name), CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal, out value);

    /// <summary>Прогноз объекта за час: тревоги (alarm=true) и типы, у которых тревоги нет (alarm=false).</summary>
    public sealed record ForecastMessage(
        int ObjectId, DateTimeOffset HourEnd, IReadOnlyList<CreatePredictionRequest> Alarms, IReadOnlyList<PredictionType> Ended);

    /// <summary>Прогноз модели: по карточке на тип с alarm=true и список типов с alarm=false. Факты, чужие
    /// часы (replay без TF_FORECAST_REPLAY) и не-JSON — null.</summary>
    public static ForecastMessage? ParseForecastMessage(string value, bool acceptReplay = false)
    {
        JsonDocument doc;
        try
        {
            doc = JsonDocument.Parse(value);
        }
        catch (JsonException)
        {
            return null;
        }

        using (doc)
        {
            var root = doc.RootElement;
            if (root.ValueKind != JsonValueKind.Object)
            {
                return null;
            }

            var clock = Text(root, "clock") ?? "live";
            if ((Text(root, "kind") ?? "forecast") != "forecast"
                || !(clock == "live" || (acceptReplay && clock == "replay"))
                || !root.TryGetProperty("object_id", out var objectEl) || !objectEl.TryGetInt32(out var objectId)
                || !TryTime(root, "hour_end", out var hourEnd)
                || !root.TryGetProperty("types", out var types) || types.ValueKind != JsonValueKind.Object)
            {
                return null;
            }

            var horizon = root.TryGetProperty("horizon_hours", out var h) && h.TryGetInt16(out var hh) ? hh : (short)24;
            var modelVersion = Text(root, "model_version");
            var result = new List<CreatePredictionRequest>();
            var ended = new List<PredictionType>();
            foreach (var entry in types.EnumerateObject())
            {
                var block = entry.Value;
                if (block.ValueKind != JsonValueKind.Object
                    || !block.TryGetProperty("alarm", out var alarm)
                    || alarm.ValueKind is not (JsonValueKind.True or JsonValueKind.False)
                    || !PredictionTypeExtensions.TryParseModel(entry.Name, out var type))
                {
                    continue;
                }

                if (alarm.ValueKind == JsonValueKind.False)
                {
                    ended.Add(type);
                    continue;
                }

                var score = Number(block, "score") ?? 0;
                var threshold = Number(block, "threshold") ?? 0;
                var confidence = Number(block, "confidence");
                var since = block.TryGetProperty("since_hours", out var s) && s.TryGetInt32(out var si) ? si : 0;
                var label = type.Label();

                result.Add(new CreatePredictionRequest
                {
                    ObjectId = objectId,
                    Type = type,
                    HourEnd = hourEnd,
                    HorizonHours = horizon,
                    Score = score,
                    Threshold = threshold,
                    Alarm = true,
                    // Вероятность — калиброванная доля подтвердившихся тревог (§2.3). score — место часа в
                    // распределении парка, не вероятность: без калибровки вероятность 0 и описание говорит об этом.
                    Probability = confidence ?? 0,
                    Confidence = confidence ?? 0,
                    SinceHours = since,
                    Topic = $"{label}: риск в ближайшие {ForecastText.Hours(horizon)}",
                    Description = ForecastText.Describe(score, threshold, confidence, since, block),
                    Classification = label,
                    Recommendation = block.TryGetProperty("recommendation", out var rec)
                        ? ForecastText.Recommendation(rec)
                        : null,
                    ModelVersionId = modelVersion,
                    Factors = Reasons(block),
                    Evidence = Evidence(block),
                });
            }

            return new ForecastMessage(objectId, hourEnd, result, ended);
        }
    }

    private static IReadOnlyList<PredictionFactorDto> Reasons(JsonElement block)
    {
        if (!block.TryGetProperty("reasons", out var reasons) || reasons.ValueKind != JsonValueKind.Array)
        {
            return Array.Empty<PredictionFactorDto>();
        }

        return reasons.EnumerateArray()
            .Where(r => r.ValueKind == JsonValueKind.Object && Text(r, "feature") is not null)
            .Select(r => new PredictionFactorDto
            {
                Feature = Text(r, "feature")!, Label = Text(r, "label"), Value = Number(r, "value") ?? 0,
            })
            .ToArray();
    }

    private static IReadOnlyList<PredictionEvidenceDto> Evidence(JsonElement block)
    {
        if (!block.TryGetProperty("evidence", out var evidence) || evidence.ValueKind != JsonValueKind.Array)
        {
            return Array.Empty<PredictionEvidenceDto>();
        }

        var result = new List<PredictionEvidenceDto>();
        foreach (var e in evidence.EnumerateArray())
        {
            if (e.ValueKind != JsonValueKind.Object
                || !e.TryGetProperty("sensor_id", out var sid) || !sid.TryGetInt32(out var sensorId)
                || !TryTime(e, "ts", out var ts))
            {
                continue;
            }

            // У дискретных каналов значение — текст («Обнаружен дым»): его кладём в ValueText.
            var number = e.TryGetProperty("value", out var v) && v.ValueKind == JsonValueKind.Number ? v.GetDouble() : (double?)null;
            var text = number is null ? Text(e, "value") : null;
            result.Add(new PredictionEvidenceDto
            {
                SensorId = sensorId, Ts = ts, Value = number,
                ValueText = text is { Length: > 100 } ? text[..100] : text,
            });
        }

        return result;
    }

    private static double? Number(JsonElement element, string name)
    {
        if (!element.TryGetProperty(name, out var value))
        {
            return null;
        }

        return value.ValueKind switch
        {
            JsonValueKind.Number => value.GetDouble(),
            JsonValueKind.String when double.TryParse(
                value.GetString(), NumberStyles.Float, CultureInfo.InvariantCulture, out var d) => d,
            _ => null,
        };
    }

    private static string? Text(JsonElement element, string name) =>
        element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() : null;
}

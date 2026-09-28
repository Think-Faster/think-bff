using BFF.Contracts.ModelSettings;

namespace BFF.WebApi.Notifications;

/// <summary>
/// Админ-панель модели (ML/INTEGRATION.md §13.3): версия типа, рабочие доли и игнорируемые периоды живут в
/// самой модели, BFF их не хранит — только проверяет право и передаёт команду. Итог команды (принят снимок
/// или отброшен как устаревший) — в /api/ml/status и в аудите модели. Здесь в БД ничего не записано,
/// поэтому сбой брокера возвращается вызывающему: админу нужно повторить.
/// </summary>
public sealed class ModelSettingsRelay
{
    private readonly IModelCommandPublisher _publisher;
    private readonly ILogger<ModelSettingsRelay> _logger;

    public ModelSettingsRelay(IModelCommandPublisher publisher, ILogger<ModelSettingsRelay> logger)
    {
        _publisher = publisher;
        _logger = logger;
    }

    public Task<ModelCommandAcceptedResponse?> SwitchAsync(HttpContext http, SwitchModelVersionRequest request, CancellationToken ct)
        => SendAsync(http, "model.switch", new { request.Type, request.VersionId, request.Reason }, ct);

    public Task<ModelCommandAcceptedResponse?> OperatingAsync(HttpContext http, OperatingSettingsRequest request, CancellationToken ct)
    {
        // reject_k — всегда ключом (null — правило отклонения выключено): модель ждёт его у каждого типа
        var types = request.Types.ToDictionary(
            t => t.Key,
            t => new Dictionary<string, double?> { ["share"] = t.Value.Share, ["reject_k"] = t.Value.RejectK });
        return SendAsync(http, "settings.operating", new { request.Version, request.Reason, Types = types }, ct);
    }

    public Task<ModelCommandAcceptedResponse?> GapsAsync(HttpContext http, IgnoredPeriodsRequest request, CancellationToken ct)
    {
        var rows = request.Rows.Select(r => new { r.A, r.B, Comment = r.Comment ?? string.Empty }).ToList();
        return SendAsync(http, "settings.gaps", new { request.Version, request.Reason, Rows = rows }, ct);
    }

    private async Task<ModelCommandAcceptedResponse?> SendAsync(HttpContext http, string kind, object payload, CancellationToken ct)
    {
        var commandId = Guid.NewGuid();
        try
        {
            await _publisher.PublishAsync(kind, commandId, payload, CommandIssuer.From(http), CommandIssuer.RequestId(http), ct);
            return new ModelCommandAcceptedResponse { CommandId = commandId, Kind = kind };
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogError(ex, "Model command {Kind} {CommandId} was not published", kind, commandId);
            return null;
        }
    }
}

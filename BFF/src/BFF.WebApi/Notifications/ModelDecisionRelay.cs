using BFF.Application.Services;
using BFF.Contracts.Incidents;
using BFF.Contracts.Predictions;
using BFF.Models.Enums;
using BFF.WebApi.Extensions;

namespace BFF.WebApi.Notifications;

/// <summary>
/// Решения диспетчера — обратно в модель (ML/INTEGRATION.md §13.3): без них у газа и подтопления не
/// работает правило отклонения, а молчание пары объект-тип модель не знает. Решение уже записано в БД;
/// сбой брокера только в журнал — повторять решение диспетчеру незачем, command_id = id решения.
/// </summary>
public sealed class ModelDecisionRelay
{
    private readonly IModelCommandPublisher _publisher;
    private readonly IPredictionService _predictions;
    private readonly ILogger<ModelDecisionRelay> _logger;

    public ModelDecisionRelay(
        IModelCommandPublisher publisher, IPredictionService predictions, ILogger<ModelDecisionRelay> logger)
    {
        _publisher = publisher;
        _predictions = predictions;
        _logger = logger;
    }

    public async Task DecisionAsync(HttpContext http, PredictionDecisionDto decision, CancellationToken ct)
    {
        var kind = decision.Action switch
        {
            DecisionAction.Take => "decision.take",
            DecisionAction.Reject => "decision.reject",
            DecisionAction.Mute => "decision.mute",
            DecisionAction.Reopen => "decision.reopen",
            _ => null,
        };
        if (kind is null)
        {
            return;
        }

        try
        {
            var prediction = await _predictions.GetAsync(decision.PredictionId, ct);
            var payload = new
            {
                PredictionId = decision.PredictionId,
                ObjectId = prediction.ObjectId,
                Type = RabbitMqModelCommandPublisher.ModelType(prediction.Type),
                ReasonCode = decision.Action == DecisionAction.Reject ? decision.ReasonCode : null,
                Until = decision.Action == DecisionAction.Mute ? decision.MutedUntil : null,
            };
            await _publisher.PublishAsync(kind, decision.Id, payload, Issuer(http), RequestId(http), ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogError(ex, "Model command {Kind} for decision {DecisionId} was not published", kind, decision.Id);
        }
    }

    public async Task ConfirmedAsync(HttpContext http, IncidentDto incident, CancellationToken ct)
    {
        try
        {
            var payload = new
            {
                ObjectId = incident.ObjectId,
                Type = RabbitMqModelCommandPublisher.ModelType(incident.Type),
                IncidentId = incident.Id,
                OccurredAt = incident.StartedAt,
            };
            await _publisher.PublishAsync("decision.confirmed", incident.Id, payload, Issuer(http), RequestId(http), ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogError(ex, "Model command decision.confirmed for incident {IncidentId} was not published", incident.Id);
        }
    }

    private static CommandIssuer Issuer(HttpContext http)
    {
        var user = http.GetCurrentUser();
        return new CommandIssuer(user?.AuthUserId ?? string.Empty, http.User.FindFirst("login")?.Value);
    }

    private static string RequestId(HttpContext http)
        => http.Request.Headers["X-Request-ID"].FirstOrDefault() ?? http.TraceIdentifier;
}

using System.Text.Json;
using BFF.WebApi.Extensions;

namespace BFF.WebApi.Audit;

/// <summary>
/// Журнал действий BFF (docs/common/права-и-аудит.md §6.2): по маршруту и методу запроса пишет
/// событие после ответа, а на любой 403 — `access.denied`. Стоит после TokenAuthenticationMiddleware
/// (кто — уже известен) и до авторизации (отказ в праве проходит через него).
/// Из тела берутся только поля из белого списка (§6.3: тела целиком не пишем).
/// </summary>
public sealed class AuditMiddleware
{
    private sealed record Rule(string Event, string ObjectType, params string[] Fields);

    // Ключ — метод и шаблон маршрута контроллера (RoutePattern.RawText) без ведущего '/'.
    private static readonly Dictionary<(string Method, string Route), Rule> Rules = new()
    {
        [("POST", "tasks")] = new("ticket.created", "ticket", "objectId", "source"),
        [("PUT", "tasks/{id:guid}")] = new("ticket.updated", "ticket", "status"),
        [("POST", "tasks/{id:guid}/take")] = new("ticket.taken", "ticket"),
        [("POST", "tasks/{id:guid}/predictions")] = new("ticket.prediction_attached", "ticket", "predictionId"),
        [("POST", "tasks/{id:guid}/assignments")] = new("ticket.assigned", "ticket"),
        [("POST", "tasks/{id:guid}/reports")] = new("ticket.commented", "ticket"),
        [("POST", "tasks/{id:guid}/returns")] = new("ticket.returned", "ticket"),
        [("POST", "predictions/{id:guid}/decisions")] = new("ticket.decided", "prediction", "action", "reasonCode", "comment"),
        [("POST", "incidents")] = new("incident.created", "incident", "objectId", "type", "predictionId", "taskId"),
        [("POST", "incidents/{id:guid}/confirm")] = new("incident.confirmed", "incident", "outcome"),
        [("GET", "objects/{id:int}")] = new("object.viewed", "object"),
        [("POST", "groups")] = new("group.changed", "group"),
        [("PUT", "groups/{id:guid}")] = new("group.changed", "group"),
        [("DELETE", "groups/{id:guid}")] = new("group.changed", "group"),
        [("POST", "groups/{id:guid}/members")] = new("group.changed", "group"),
        [("POST", "groups/{id:guid}/members/batch")] = new("group.changed", "group"),
        [("DELETE", "groups/{id:guid}/members/{memberType}/{memberId:guid}")] = new("group.changed", "group"),
        [("POST", "permissions/grants")] = new("role.changed", "grant"),
        [("DELETE", "permissions/grants/{id:guid}")] = new("role.changed", "grant"),
        [("POST", "users")] = new("user.created", "user"),
        [("DELETE", "users/{id:guid}")] = new("user.blocked", "user"),
        [("POST", "model-versions/{id}/activate")] = new("model.switched", "model_version"),
        [("POST", "coefficients")] = new("threshold.changed", "coefficient"),
        [("POST", "ignored-ranges")] = new("model.muted", "ignored_range"),
        [("DELETE", "ignored-ranges/{id:guid}")] = new("model.unmuted", "ignored_range"),
        [("POST", "notifications/email")] = new("notification.email_sent", "notification", "subject"),
    };

    private readonly RequestDelegate _next;
    private readonly AuditWriter _audit;

    public AuditMiddleware(RequestDelegate next, AuditWriter audit)
    {
        _next = next;
        _audit = audit;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var route = (context.GetEndpoint() as RouteEndpoint)?.RoutePattern.RawText?.Trim('/');
        Rule? rule = null;
        if (route is not null)
        {
            Rules.TryGetValue((context.Request.Method.ToUpperInvariant(), route), out rule);
        }

        Dictionary<string, object?>? body = null;
        if (rule is { Fields.Length: > 0 })
        {
            body = await ReadFieldsAsync(context.Request, rule.Fields);
        }

        await _next(context);

        var status = context.Response.StatusCode;
        if (status == StatusCodes.Status403Forbidden)
        {
            await _audit.WriteAsync(Build(context, "access.denied", "denied", rule?.ObjectType, new Dictionary<string, object?>
            {
                ["method"] = context.Request.Method,
                ["route"] = route ?? context.Request.Path.Value,
                ["action"] = rule?.Event,
            }));
            return;
        }

        if (rule is null || status == StatusCodes.Status401Unauthorized)
        {
            return;
        }

        var details = body ?? new Dictionary<string, object?>();
        var outcome = status < 400 ? "success" : "error";
        if (outcome == "error")
        {
            details["status"] = status;
        }

        await _audit.WriteAsync(Build(context, rule.Event, outcome, rule.ObjectType, details));
    }

    private static AuditEvent Build(
        HttpContext context, string eventType, string outcome, string? objectType, Dictionary<string, object?> details)
    {
        var user = context.GetCurrentUser();
        var objectId = context.GetRouteValue("id")?.ToString();
        return new AuditEvent(
            eventType,
            outcome,
            user is null ? "anonymous" : "user",
            user?.AuthUserId,
            context.User.FindFirst("login")?.Value,
            context.Request.Headers["X-Request-ID"].FirstOrDefault() ?? context.TraceIdentifier,
            ClientIp(context),
            objectType,
            objectId,
            details);
    }

    private static string? ClientIp(HttpContext context)
    {
        // За nginx адрес клиента — первый в X-Forwarded-For (или X-Real-IP), иначе адрес соединения.
        var forwarded = context.Request.Headers["X-Forwarded-For"].FirstOrDefault()?.Split(',')[0].Trim();
        return !string.IsNullOrEmpty(forwarded)
            ? forwarded
            : context.Request.Headers["X-Real-IP"].FirstOrDefault() ?? context.Connection.RemoteIpAddress?.ToString();
    }

    private static async Task<Dictionary<string, object?>> ReadFieldsAsync(HttpRequest request, string[] fields)
    {
        var picked = new Dictionary<string, object?>();
        if (request.ContentLength is null or 0 || request.ContentType?.Contains("json") != true)
        {
            return picked;
        }

        request.EnableBuffering();
        try
        {
            using var doc = await JsonDocument.ParseAsync(request.Body);
            if (doc.RootElement.ValueKind != JsonValueKind.Object)
            {
                return picked;
            }

            foreach (var property in doc.RootElement.EnumerateObject())
            {
                var name = fields.FirstOrDefault(f => string.Equals(f, property.Name, StringComparison.OrdinalIgnoreCase));
                if (name is not null)
                {
                    picked[JsonNamingPolicy.SnakeCaseLower.ConvertName(name)] = property.Value.ValueKind switch
                    {
                        JsonValueKind.String => property.Value.GetString(),
                        JsonValueKind.Number => property.Value.GetRawText(),
                        JsonValueKind.True => true,
                        JsonValueKind.False => false,
                        _ => null,
                    };
                }
            }
        }
        catch (JsonException)
        {
            // Сломанное тело отвергнет валидатор контроллера; журналу хватит события без подробностей.
        }
        finally
        {
            request.Body.Position = 0;
        }

        return picked;
    }
}

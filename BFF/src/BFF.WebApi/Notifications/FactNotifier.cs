using System.Globalization;
using BFF.Application.Exceptions;
using BFF.Application.Services;
using BFF.Contracts.Predictions;
using BFF.Models.Enums;
using BFF.WebApi.Audit;

namespace BFF.WebApi.Notifications;

/// <summary>Что известно о событии по факту сверх записи FactAlert: пометка модели (например «идёт ППР по
/// графику») и номер строки графика работ.</summary>
public sealed record FactContext(string? Note = null, long? WorkId = null, string? RequestId = null);

/// <summary>
/// Уведомление по факту: всем, кто сейчас на смене по графику и не в отпуске (IDutyService), — письмо на
/// почту и сообщение в Telegram через tf.notifications (tf-mail и tf-tg в think-infra). Письмо — отдельным
/// сообщением на адресата, как у POST /notifications/email: получатели не видят друг друга. Telegram —
/// одно сообщение на все имена (`to.usernames`), tf-tg шлёт каждому личное; кто не подключил бота, тому
/// не уйдёт, остальным уйдёт. Ограничение «письмо в минуту» здесь не действует:
/// событие шлёт система, а не человек, и одно происшествие не должно теряться из-за соседнего.
/// </summary>
public sealed class FactNotifier
{
    private static readonly TimeSpan Msk = TimeSpan.FromHours(3);

    private readonly IDutyService _duty;
    private readonly IObjectService _objects;
    private readonly INoticePublisher _publisher;
    private readonly AuditWriter _audit;
    private readonly ILogger<FactNotifier> _logger;

    public FactNotifier(
        IDutyService duty,
        IObjectService objects,
        INoticePublisher publisher,
        AuditWriter audit,
        ILogger<FactNotifier> logger)
    {
        _duty = duty;
        _objects = objects;
        _publisher = publisher;
        _audit = audit;
        _logger = logger;
    }

    public async Task NotifyAsync(FactAlertDto alert, FactContext context, CancellationToken ct)
    {
        var recipients = await _duty.OnDutyAsync(DateTimeOffset.UtcNow, ct);
        var (subject, text) = await ComposeAsync(alert, context, ct);
        var ticketId = alert.Id.ToString();

        var emails = recipients
            .Select(r => r.Email)
            .OfType<string>()
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        var usernames = recipients
            .Select(r => r.Telegram)
            .OfType<string>()
            .Distinct()
            .ToList();

        var sent = 0;
        var failed = 0;
        foreach (var email in emails)
        {
            var notice = new EmailNotice(1, Guid.NewGuid(), subject, text, new NoticeTo(new[] { email }),
                ticketId, "fact", context.RequestId);
            if (await TryAsync(() => _publisher.PublishEmailAsync(notice, ct), notice.NoticeId))
            {
                sent++;
            }
            else
            {
                failed++;
            }
        }

        var telegram = false;
        if (usernames.Count > 0)
        {
            var notice = new TelegramNotice(1, Guid.NewGuid(), subject, text, new TelegramTo(Usernames: usernames),
                ticketId, "fact", context.RequestId);
            telegram = await TryAsync(() => _publisher.PublishTelegramAsync(notice, ct), notice.NoticeId);
        }

        _logger.LogInformation(
            "Fact alert {AlertId} (object {ObjectId}, {Type}): on duty {OnDuty}, emails {Sent}/{Emails}, telegram users {Telegram} ({TelegramState})",
            alert.Id, alert.ObjectId, alert.Type, recipients.Count, sent, emails.Count, usernames.Count,
            usernames.Count == 0 ? "none" : telegram ? "queued" : "failed");

        await _audit.WriteAsync(new AuditEvent(
            "ticket.created", failed == 0 && (usernames.Count == 0 || telegram) ? "success" : "error",
            "service", null, "tf-bff", context.RequestId, null, "fact_alert", alert.Id.ToString(),
            new Dictionary<string, object?>
            {
                ["source"] = "fact",
                ["object_id"] = alert.ObjectId,
                ["type"] = alert.Type.ToString(),
                ["started_at"] = alert.StartedAt,
                ["note"] = context.Note,
                ["on_duty"] = recipients.Count,
                ["emails_queued"] = sent,
                ["emails_failed"] = failed,
                ["telegram_users"] = telegram ? usernames.Count : 0,
            }));
    }

    private async Task<bool> TryAsync(Func<Task> publish, Guid noticeId)
    {
        try
        {
            await publish();
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to publish fact notice {NoticeId}", noticeId);
            return false;
        }
    }

    private async Task<(string Subject, string Text)> ComposeAsync(FactAlertDto alert, FactContext context, CancellationToken ct)
    {
        string place;
        try
        {
            var obj = await _objects.GetAsync(alert.ObjectId, ct);
            place = string.IsNullOrWhiteSpace(obj.Address) ? obj.Name : $"{obj.Name}, {obj.Address}";
        }
        catch (NotFoundException)
        {
            place = $"объект {alert.ObjectId}";
        }

        var type = alert.Type.Label();
        var group = alert.Group.Label();
        var started = alert.StartedAt.ToOffset(Msk).ToString("dd.MM.yyyy HH:mm", CultureInfo.InvariantCulture);
        var lines = new List<string>
        {
            $"{group} по факту: {type.ToLowerInvariant()}.",
            $"Объект: {place}.",
            $"Началось: {started} МСК.",
        };
        // Слепой объект не даёт показаний — за потерей связи или питания может стоять авария (§13.11).
        if (alert.Type == PredictionType.Blind)
        {
            lines.Add("Объект не видно — возможна авария.");
        }
        if (Where(alert) is { } where)
        {
            lines.Add(where);
        }

        if (!string.IsNullOrWhiteSpace(context.Note))
        {
            lines.Add(context.WorkId is { } work ? $"{context.Note} (работа {work})." : $"{context.Note}.");
        }

        lines.Add($"Номер эпизода в журнале данных: {alert.Id}.");
        var subject = alert.Type == PredictionType.Blind
            ? $"{group}, возможна авария — {type.ToLowerInvariant()}: {place}"
            : $"{group} — {type.ToLowerInvariant()}: {place}";
        return (subject, string.Join('\n', lines));
    }

    // Сколько датчиков перечислять в письме: дальше — числом, полный список в журнале данных.
    private const int MaxListedSensors = 5;

    /// <summary>Где искать: сработавшие датчики с пикетом из справочника (BFF подставляет при записи).</summary>
    private static string? Where(FactAlertDto alert)
    {
        if (alert.Sensors.Count == 0)
        {
            return null;
        }

        var listed = alert.Sensors.Take(MaxListedSensors).Select(s =>
        {
            var name = string.IsNullOrWhiteSpace(s.Name) ? $"датчик №{s.SensorId}" : s.Name;
            return s.PicketCode is { Length: > 0 } picket ? $"{name} ({picket})" : name;
        }).ToList();
        var rest = alert.Sensors.Count - listed.Count;
        return $"Сработали: {string.Join(", ", listed)}{(rest > 0 ? $" и ещё {rest}" : "")}.";
    }
}

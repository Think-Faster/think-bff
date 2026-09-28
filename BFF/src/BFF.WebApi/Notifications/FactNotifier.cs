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
/// одно сообщение на все chat_id с тем же notice_id. Ограничение «письмо в минуту» здесь не действует:
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
        var chats = recipients
            .Select(r => ChatId(r.Telegram))
            .OfType<object>()
            .Distinct()
            .ToList();
        var skipped = recipients.Count(r => r.Telegram is not null && ChatId(r.Telegram) is null);

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
        if (chats.Count > 0)
        {
            var notice = new TelegramNotice(1, Guid.NewGuid(), subject, text, new TelegramTo(chats),
                ticketId, "fact", context.RequestId);
            telegram = await TryAsync(() => _publisher.PublishTelegramAsync(notice, ct), notice.NoticeId);
        }

        _logger.LogInformation(
            "Fact alert {AlertId} (object {ObjectId}, {Type}): on duty {OnDuty}, emails {Sent}/{Emails}, telegram chats {Chats} ({TelegramState}), telegram not a chat_id {Skipped}",
            alert.Id, alert.ObjectId, alert.Type, recipients.Count, sent, emails.Count, chats.Count,
            chats.Count == 0 ? "none" : telegram ? "queued" : "failed", skipped);

        await _audit.WriteAsync(new AuditEvent(
            "ticket.created", failed == 0 && (chats.Count == 0 || telegram) ? "success" : "failure",
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
                ["telegram_chats"] = telegram ? chats.Count : 0,
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

        var type = TypeName(alert.Type);
        var started = alert.StartedAt.ToOffset(Msk).ToString("dd.MM.yyyy HH:mm", CultureInfo.InvariantCulture);
        var lines = new List<string>
        {
            $"Происшествие по факту: {type.ToLowerInvariant()}.",
            $"Объект: {place}.",
            $"Началось: {started} МСК.",
        };
        if (!string.IsNullOrWhiteSpace(context.Note))
        {
            lines.Add(context.WorkId is { } work ? $"{context.Note} (работа {work})." : $"{context.Note}.");
        }

        lines.Add($"Заявка по факту: {alert.Id}.");
        return ($"{type}: {place}", string.Join('\n', lines));
    }

    // chat_id — целое число (у групп отрицательное) или "@канал". Имя пользователя (@login) Bot API
    // не принимает — такое значение tf-tg отклонит, но остальным чатам сообщение всё равно уйдёт.
    private static object? ChatId(string? telegram)
    {
        if (telegram is null)
        {
            return null;
        }

        if (long.TryParse(telegram, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out var id))
        {
            return id;
        }

        return telegram.StartsWith('@') && telegram.Length > 1 ? telegram : null;
    }

    private static string TypeName(PredictionType type) => type switch
    {
        PredictionType.Fire => "Пожар",
        PredictionType.Gas => "Загазованность",
        PredictionType.Flood => "Подтопление",
        PredictionType.EquipmentFailure => "Отказ оборудования",
        PredictionType.SensorFailure => "Отказ датчика",
        PredictionType.Intrusion => "Проникновение",
        _ => type.ToString(),
    };
}

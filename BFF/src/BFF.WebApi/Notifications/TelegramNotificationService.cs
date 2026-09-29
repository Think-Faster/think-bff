using System.Text.RegularExpressions;
using BFF.Application.Services;
using BFF.Contracts.Notifications;
using BFF.Models.Enums;
using RabbitMQ.Client.Exceptions;

namespace BFF.WebApi.Notifications;

/// <summary>Оркестрирует POST /notifications/telegram — как EmailNotificationService у почты: userId →
/// имя в Telegram (IUserService), дубликаты по имени схлопываются, «раз в минуту на имя» (Redis), и одно
/// сообщение tf.notifications с routing key "telegram" на всех получателей (`to.usernames`): tf-tg шлёт
/// каждому своё личное сообщение, получатели друг друга не видят. Кто точно не подключил бота (связи нет
/// в Redis tf-tg) — notLinked, в очередь не ставится. Scoped — зависит от scoped IUserService.</summary>
public sealed class TelegramNotificationService
{
    private const int MaxSubjectLength = 255;

    // Предел одного сообщения Telegram; tf-tg считает его по готовому тексту
    // `<b>тема</b>\n\nтекст` с HTML-экранированием (think-infra/telegram/app/telegram_bot.py).
    private const int MessageLimit = 4096;
    private const int MarkupLength = 9;

    private readonly IUserService _userService;
    private readonly INoticePublisher _publisher;
    private readonly NotificationRateLimiter _rateLimiter;
    private readonly TelegramLinks _links;
    private readonly ILogger<TelegramNotificationService> _logger;

    public TelegramNotificationService(
        IUserService userService,
        INoticePublisher publisher,
        NotificationRateLimiter rateLimiter,
        TelegramLinks links,
        ILogger<TelegramNotificationService> logger)
    {
        _userService = userService;
        _publisher = publisher;
        _rateLimiter = rateLimiter;
        _links = links;
        _logger = logger;
    }

    public async Task<SendTelegramResponse> SendAsync(SendTelegramRequest request, string? requestId, CancellationToken ct)
    {
        var results = new List<TelegramRecipientResultDto>();
        var pending = new Dictionary<string, Guid>();
        var duplicates = new List<(Guid UserId, string Username)>();

        foreach (var r in await _userService.ResolveTelegramsAsync(request.UserIds, ct))
        {
            if (!r.Found)
            {
                results.Add(new TelegramRecipientResultDto { UserId = r.UserId, Status = TelegramSendStatus.UserNotFound });
            }
            else if (r.Telegram is null)
            {
                results.Add(new TelegramRecipientResultDto { UserId = r.UserId, Status = TelegramSendStatus.NoTelegramOnFile });
            }
            else if (!pending.TryAdd(r.Telegram, r.UserId))
            {
                // У двух пользователей одно имя в Telegram — сообщение одно, статус как у первого.
                duplicates.Add((r.UserId, r.Telegram));
            }
        }

        var statuses = new Dictionary<string, TelegramSendStatus>();
        var linked = await _links.LinkedAsync(pending.Keys, ct);
        var usernames = new List<string>();
        foreach (var username in pending.Keys)
        {
            if (linked is not null && !linked.GetValueOrDefault(username))
            {
                statuses[username] = TelegramSendStatus.NotLinked;
            }
            else if (!await _rateLimiter.TryAcquireTelegramAsync(username, ct))
            {
                statuses[username] = TelegramSendStatus.RateLimited;
            }
            else
            {
                usernames.Add(username);
            }
        }

        if (usernames.Count > 0)
        {
            var subject = NormalizeSubject(request.Subject);
            var notice = new TelegramNotice(
                Schema: 1,
                NoticeId: Guid.NewGuid(),
                Subject: subject,
                Text: Fit(subject, request.Text),
                To: new TelegramTo(Usernames: usernames),
                TicketId: request.TicketId,
                Kind: request.Kind,
                RequestId: requestId);

            var status = TelegramSendStatus.Sent;
            try
            {
                await _publisher.PublishTelegramAsync(notice, ct);
            }
            catch (PublishException ex)
            {
                _logger.LogWarning(ex, "RabbitMQ rejected telegram notice {NoticeId} (nack/basic.return)", notice.NoticeId);
                status = TelegramSendStatus.Failed;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to publish telegram notice {NoticeId}", notice.NoticeId);
                status = TelegramSendStatus.Failed;
            }

            foreach (var username in usernames)
            {
                statuses[username] = status;
            }
        }

        results.AddRange(pending.Select(p => new TelegramRecipientResultDto { UserId = p.Value, Username = p.Key, Status = statuses[p.Key] }));
        results.AddRange(duplicates.Select(d => new TelegramRecipientResultDto { UserId = d.UserId, Username = d.Username, Status = statuses[d.Username] }));

        return new SendTelegramResponse
        {
            Requested = results.Count,
            Sent = results.Count(r => r.Status == TelegramSendStatus.Sent),
            Results = results,
        };
    }

    private static string NormalizeSubject(string subject)
    {
        var value = Regex.Replace(subject.Trim(), @"\s+", " ");
        return value.Length > MaxSubjectLength ? value[..MaxSubjectLength] : value;
    }

    // Текст укорачивается так, чтобы готовое сообщение влезло в предел Telegram, — иначе tf-tg его отклонит.
    private static string Fit(string subject, string text)
    {
        var room = MessageLimit - MarkupLength - EscapedLength(subject);
        if (EscapedLength(text) <= room)
        {
            return text;
        }

        room -= 1; // «…»
        var length = 0;
        var end = 0;
        while (end < text.Length && length + EscapedLength(text[end]) <= room)
        {
            length += EscapedLength(text[end]);
            end++;
        }

        if (end > 0 && char.IsHighSurrogate(text[end - 1]))
        {
            end--;
        }

        return text[..end] + "…";
    }

    private static int EscapedLength(string value) => value.Sum(EscapedLength);

    // html.escape(quote=True): & → &amp;  < → &lt;  > → &gt;  " → &quot;  ' → &#x27;
    private static int EscapedLength(char c) => c switch
    {
        '&' => 5,
        '<' or '>' => 4,
        '"' or '\'' => 6,
        _ => 1,
    };
}

using System.Net.Mail;
using System.Text.RegularExpressions;
using BFF.Application.Services;
using BFF.Contracts.Notifications;
using BFF.Models.Enums;
using RabbitMQ.Client.Exceptions;

namespace BFF.WebApi.Notifications;

/// <summary>Оркестрирует POST /notifications/email: резолвит UserIds в email через IUserService (у
/// WebApi нет прямого доступа к BffDbContext — раздел 3 ТЗ), схлопывает дубликаты между UserIds и
/// Emails по итоговому адресу, лимитирует (Redis, свой барьер — раздел "Неоднозначности" в
/// docs/DECISIONS.md) и публикует одно сообщение tf.notifications на каждого получателя (раздел 4.2
/// задания инфраструктуры — получатели одного сообщения видят друг друга в "Кому", у нас это не
/// нужно, поэтому один получатель = одно сообщение со своим notice_id). Scoped — зависит от scoped
/// IUserService.</summary>
public sealed class EmailNotificationService
{
    private const int MaxSubjectLength = 255;
    private const int MaxTextLength = 20_000;

    private readonly IUserService _userService;
    private readonly INoticePublisher _publisher;
    private readonly EmailRateLimiter _rateLimiter;
    private readonly ILogger<EmailNotificationService> _logger;

    public EmailNotificationService(
        IUserService userService,
        INoticePublisher publisher,
        EmailRateLimiter rateLimiter,
        ILogger<EmailNotificationService> logger)
    {
        _userService = userService;
        _publisher = publisher;
        _rateLimiter = rateLimiter;
        _logger = logger;
    }

    public async Task<SendEmailResponse> SendAsync(SendEmailRequest request, string? requestId, CancellationToken ct)
    {
        var results = new List<EmailRecipientResultDto>();
        // Email -> userId (если известен) для тех, кого ещё предстоит лимитировать и отправить.
        // Дубликаты (тот же адрес пришёл и через userIds, и через emails, или повторился в одном из
        // списков) схлопываются в одну попытку отправки — конкретно к этому и сводится требование
        // "1 письмо в минуту на адрес": считать один и тот же адрес дважды в одном запросе бессмысленно.
        var pending = new Dictionary<string, Guid?>(StringComparer.OrdinalIgnoreCase);

        if (request.UserIds is { Count: > 0 })
        {
            var resolved = await _userService.ResolveEmailsAsync(request.UserIds, ct);
            foreach (var r in resolved)
            {
                if (!r.Found)
                {
                    results.Add(new EmailRecipientResultDto { UserId = r.UserId, Status = EmailSendStatus.UserNotFound });
                }
                else if (string.IsNullOrWhiteSpace(r.Email))
                {
                    results.Add(new EmailRecipientResultDto { UserId = r.UserId, Status = EmailSendStatus.NoEmailOnFile });
                }
                else
                {
                    pending.TryAdd(r.Email.Trim(), r.UserId);
                }
            }
        }

        if (request.Emails is { Count: > 0 })
        {
            foreach (var raw in request.Emails)
            {
                var email = raw.Trim();
                if (!IsValidEmail(email))
                {
                    results.Add(new EmailRecipientResultDto { Email = email, Status = EmailSendStatus.InvalidEmail });
                }
                else
                {
                    pending.TryAdd(email, null);
                }
            }
        }

        var subject = NormalizeSubject(request.Subject);
        var text = Truncate(request.Text, MaxTextLength);

        foreach (var (email, userId) in pending)
        {
            if (!await _rateLimiter.TryAcquireAsync(email, ct))
            {
                results.Add(new EmailRecipientResultDto { Email = email, UserId = userId, Status = EmailSendStatus.RateLimited });
                continue;
            }

            var notice = new EmailNotice(
                Schema: 1,
                NoticeId: Guid.NewGuid(),
                Subject: subject,
                Text: text,
                To: new NoticeTo(new[] { email }),
                TicketId: request.TicketId,
                Kind: request.Kind,
                RequestId: requestId);

            try
            {
                await _publisher.PublishEmailAsync(notice, ct);
                results.Add(new EmailRecipientResultDto { Email = email, UserId = userId, Status = EmailSendStatus.Sent });
            }
            catch (PublishException ex)
            {
                _logger.LogWarning(ex, "RabbitMQ rejected notice {NoticeId} for {Email} (nack/basic.return)", notice.NoticeId, email);
                results.Add(new EmailRecipientResultDto { Email = email, UserId = userId, Status = EmailSendStatus.Failed });
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to publish notice {NoticeId} for {Email}", notice.NoticeId, email);
                results.Add(new EmailRecipientResultDto { Email = email, UserId = userId, Status = EmailSendStatus.Failed });
            }
        }

        return new SendEmailResponse
        {
            Requested = results.Count,
            Sent = results.Count(r => r.Status == EmailSendStatus.Sent),
            Results = results,
        };
    }

    private static bool IsValidEmail(string email)
    {
        try
        {
            _ = new MailAddress(email);
            return true;
        }
        catch (FormatException)
        {
            return false;
        }
    }

    // Раздел 3.2 задания: переводы строк и повторные пробелы в subject заменяются одним пробелом
    // (text — нет, там перевод строки значащий, \n).
    private static string NormalizeSubject(string subject) =>
        Truncate(Regex.Replace(subject.Trim(), @"\s+", " "), MaxSubjectLength);

    private static string Truncate(string value, int maxLength) =>
        value.Length > maxLength ? value[..maxLength] : value;
}

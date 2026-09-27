using System.Net.Mail;
using BFF.Application.Services;
using BFF.Contracts.Notifications;
using BFF.Models.Enums;

namespace BFF.WebApi.Notifications;

/// <summary>Оркестрирует POST /notifications/email: резолвит UserIds в email через IUserService (у
/// WebApi нет прямого доступа к BffDbContext — раздел 3 ТЗ), схлопывает дубликаты между UserIds и
/// Emails по итоговому адресу, лимитирует и отправляет. Scoped — зависит от scoped IUserService.</summary>
public sealed class EmailNotificationService
{
    private readonly IUserService _userService;
    private readonly IEmailSender _sender;
    private readonly EmailRateLimiter _rateLimiter;
    private readonly ILogger<EmailNotificationService> _logger;

    public EmailNotificationService(
        IUserService userService,
        IEmailSender sender,
        EmailRateLimiter rateLimiter,
        ILogger<EmailNotificationService> logger)
    {
        _userService = userService;
        _sender = sender;
        _rateLimiter = rateLimiter;
        _logger = logger;
    }

    public async Task<SendEmailResponse> SendAsync(SendEmailRequest request, CancellationToken ct)
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

        foreach (var (email, userId) in pending)
        {
            if (!await _rateLimiter.TryAcquireAsync(email, ct))
            {
                results.Add(new EmailRecipientResultDto { Email = email, UserId = userId, Status = EmailSendStatus.RateLimited });
                continue;
            }

            try
            {
                await _sender.SendAsync(email, request.Subject, request.Body, request.IsHtml, ct);
                results.Add(new EmailRecipientResultDto { Email = email, UserId = userId, Status = EmailSendStatus.Sent });
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to send email to {Email}", email);
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
}

using BFF.Contracts.Notifications;
using BFF.Models.Constants;
using BFF.Models.Enums;
using BFF.WebApi.Authorization;
using BFF.WebApi.Notifications;
using FluentValidation;
using Microsoft.AspNetCore.Mvc;

namespace BFF.WebApi.Controllers;

[ApiController]
[Route("notifications")]
public sealed class NotificationsController : ControllerBase
{
    private readonly EmailNotificationService _emailNotificationService;
    private readonly TelegramNotificationService _telegramNotificationService;
    private readonly IValidator<SendEmailRequest> _sendEmailValidator;
    private readonly IValidator<SendTelegramRequest> _sendTelegramValidator;

    public NotificationsController(
        EmailNotificationService emailNotificationService,
        TelegramNotificationService telegramNotificationService,
        IValidator<SendEmailRequest> sendEmailValidator,
        IValidator<SendTelegramRequest> sendTelegramValidator)
    {
        _emailNotificationService = emailNotificationService;
        _telegramNotificationService = telegramNotificationService;
        _sendEmailValidator = sendEmailValidator;
        _sendTelegramValidator = sendTelegramValidator;
    }

    [HttpPost("email")]
    [RequirePermission(ResourceCodes.Notifications, PermissionFlags.Create)]
    public async Task<IActionResult> SendEmail([FromBody] SendEmailRequest request, CancellationToken ct)
    {
        await _sendEmailValidator.ValidateAndThrowAsync(request, ct);
        // Сквозная трассировка до логов tf-mail (раздел 3.2/7 задания инфраструктуры) — тот же
        // источник, что и у AuditMiddleware.
        var requestId = HttpContext.Request.Headers["X-Request-ID"].FirstOrDefault() ?? HttpContext.TraceIdentifier;
        var result = await _emailNotificationService.SendAsync(request, requestId, ct);
        return Ok(result);
    }

    /// <summary>Сообщение от бота пользователям по их именам в Telegram — то же право, что у письма.</summary>
    [HttpPost("telegram")]
    [RequirePermission(ResourceCodes.Notifications, PermissionFlags.Create)]
    public async Task<IActionResult> SendTelegram([FromBody] SendTelegramRequest request, CancellationToken ct)
    {
        await _sendTelegramValidator.ValidateAndThrowAsync(request, ct);
        var requestId = HttpContext.Request.Headers["X-Request-ID"].FirstOrDefault() ?? HttpContext.TraceIdentifier;
        var result = await _telegramNotificationService.SendAsync(request, requestId, ct);
        return Ok(result);
    }
}

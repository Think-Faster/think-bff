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
    private readonly IValidator<SendEmailRequest> _sendEmailValidator;

    public NotificationsController(
        EmailNotificationService emailNotificationService,
        IValidator<SendEmailRequest> sendEmailValidator)
    {
        _emailNotificationService = emailNotificationService;
        _sendEmailValidator = sendEmailValidator;
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
}

using BFF.Models.Enums;

namespace BFF.Contracts.Notifications;

/// <summary>Хотя бы один получатель обязателен — либо в UserIds, либо в Emails, либо в обоих сразу
/// (дубликаты по итоговому email-адресу схлопываются в один запрос на отправку).</summary>
public sealed class SendEmailRequest
{
    public string Subject { get; init; } = string.Empty;
    public string Body { get; init; } = string.Empty;
    public bool IsHtml { get; init; }
    public IReadOnlyList<Guid>? UserIds { get; init; }
    public IReadOnlyList<string>? Emails { get; init; }
}

public sealed class EmailRecipientResultDto
{
    public string Email { get; init; } = string.Empty;
    public Guid? UserId { get; init; }
    public EmailSendStatus Status { get; init; }
}

public sealed class SendEmailResponse
{
    public int Requested { get; init; }
    public int Sent { get; init; }
    public IReadOnlyList<EmailRecipientResultDto> Results { get; init; } = Array.Empty<EmailRecipientResultDto>();
}

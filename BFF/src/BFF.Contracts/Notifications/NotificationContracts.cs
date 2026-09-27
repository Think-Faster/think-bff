using BFF.Models.Enums;

namespace BFF.Contracts.Notifications;

/// <summary>Хотя бы один получатель обязателен — либо в UserIds, либо в Emails, либо в обоих сразу
/// (дубликаты по итоговому email-адресу схлопываются в одну публикацию). Text — только простой текст:
/// письмо уходит через tf-mail, HTML не поддерживается и дойдёт как есть, тегами.</summary>
public sealed class SendEmailRequest
{
    public string Subject { get; init; } = string.Empty;
    public string Text { get; init; } = string.Empty;
    public IReadOnlyList<Guid>? UserIds { get; init; }
    public IReadOnlyList<string>? Emails { get; init; }

    /// <summary>Необязательно — только для логов tf-mail (номер заявки), на доставку не влияет.</summary>
    public string? TicketId { get; init; }

    /// <summary>Необязательно — только для логов tf-mail (например "fact"/"forecast"), на доставку не влияет.</summary>
    public string? Kind { get; init; }
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

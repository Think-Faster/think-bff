namespace BFF.Models.Entities;

public sealed class User
{
    public Guid Id { get; set; }
    public string AuthUserId { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string FirstName { get; set; } = string.Empty;
    public string? MiddleName { get; set; }
    public string? Email { get; set; }

    /// <summary>Имя в Telegram без @, в нижнем регистре (TelegramUsername) — куда слать уведомления.</summary>
    public string? Telegram { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}

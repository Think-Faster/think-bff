using BFF.Models.Enums;

namespace BFF.Models.Entities;

public sealed class EngineerProfile
{
    public Guid UserId { get; set; }
    public Guid? BrigadeId { get; set; }
    public string? Phone { get; set; }
    /// <summary>До 29.09 — Telegram инженера. Теперь Telegram у любого пользователя — users.telegram, а
    /// колонка оставлена как история (миграция AddUserTelegram перенесла из неё имена пользователей).</summary>
    public string? Telegram { get; set; }
    public string[] Specialization { get; set; } = Array.Empty<string>();
    public EngineerStatus Status { get; set; } = EngineerStatus.Unavailable;
}

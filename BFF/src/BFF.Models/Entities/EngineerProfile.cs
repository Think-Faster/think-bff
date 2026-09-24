using BFF.Models.Enums;

namespace BFF.Models.Entities;

public sealed class EngineerProfile
{
    public Guid UserId { get; set; }
    public Guid? BrigadeId { get; set; }
    public string? Phone { get; set; }
    public string? Telegram { get; set; }
    public string[] Specialization { get; set; } = Array.Empty<string>();
    public EngineerStatus Status { get; set; } = EngineerStatus.Unavailable;
}

namespace BFF.Application.Services;

/// <summary>Кому слать уведомление по факту: сотрудник сейчас на смене по графику и не в отпуске, и у него
/// указан хотя бы один канал — почта (users.email) или Telegram (engineer_profiles.telegram).</summary>
public sealed record DutyRecipient(Guid UserId, string? Email, string? Telegram);

public interface IDutyService
{
    Task<IReadOnlyList<DutyRecipient>> OnDutyAsync(DateTimeOffset now, CancellationToken ct);
}

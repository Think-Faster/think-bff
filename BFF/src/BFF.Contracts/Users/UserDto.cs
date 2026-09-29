using BFF.Contracts.Groups;

namespace BFF.Contracts.Users;

public sealed class UserDto
{
    public Guid Id { get; init; }
    public string AuthUserId { get; init; } = string.Empty;
    public string LastName { get; init; } = string.Empty;
    public string FirstName { get; init; } = string.Empty;
    public string? MiddleName { get; init; }
    public string? Email { get; init; }
    /// <summary>Имя в Telegram без @ (нижний регистр). Бот шлёт уведомления, когда человек нажал у него «Старт».</summary>
    public string? Telegram { get; init; }
    public bool IsActive { get; init; }
    public IReadOnlyList<GroupRefDto> Groups { get; init; } = Array.Empty<GroupRefDto>();
}

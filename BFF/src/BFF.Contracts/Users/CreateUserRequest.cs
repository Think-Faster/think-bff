namespace BFF.Contracts.Users;

public sealed class CreateUserRequest
{
    public string AuthUserId { get; init; } = string.Empty;
    public string LastName { get; init; } = string.Empty;
    public string FirstName { get; init; } = string.Empty;
    public string? MiddleName { get; init; }
    public string? Email { get; init; }
    /// <summary>Имя в Telegram (можно с @) — сохраняется без @, в нижнем регистре.</summary>
    public string? Telegram { get; init; }
    public IReadOnlyList<Guid>? GroupIds { get; init; }
}

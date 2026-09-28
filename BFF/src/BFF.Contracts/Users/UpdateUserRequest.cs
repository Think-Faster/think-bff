namespace BFF.Contracts.Users;

public sealed class UpdateUserRequest
{
    public string LastName { get; init; } = string.Empty;
    public string FirstName { get; init; } = string.Empty;
    public string? MiddleName { get; init; }
    public string? Email { get; init; }
    /// <summary>Имя в Telegram (можно с @). null — не менять, пустая строка — убрать.</summary>
    public string? Telegram { get; init; }
    public bool IsActive { get; init; }
}

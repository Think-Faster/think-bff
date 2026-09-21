namespace BFF.Contracts.Users;

public sealed class UserListItemDto
{
    public Guid Id { get; init; }
    public string AuthUserId { get; init; } = string.Empty;
    public string LastName { get; init; } = string.Empty;
    public string FirstName { get; init; } = string.Empty;
    public string? MiddleName { get; init; }
    public bool IsActive { get; init; }
}

namespace BFF.Contracts.Users;

public sealed class UpdateUserRequest
{
    public string LastName { get; init; } = string.Empty;
    public string FirstName { get; init; } = string.Empty;
    public string? MiddleName { get; init; }
    public string? Email { get; init; }
    public bool IsActive { get; init; }
}

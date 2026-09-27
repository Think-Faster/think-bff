namespace BFF.Contracts.Users;

public sealed class CreateUserRequest
{
    public string AuthUserId { get; init; } = string.Empty;
    public string LastName { get; init; } = string.Empty;
    public string FirstName { get; init; } = string.Empty;
    public string? MiddleName { get; init; }
    public IReadOnlyList<Guid>? GroupIds { get; init; }
}

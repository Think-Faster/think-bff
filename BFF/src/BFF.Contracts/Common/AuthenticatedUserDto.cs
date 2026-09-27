namespace BFF.Contracts.Common;

/// <summary>Minimal projection TokenAuthenticationMiddleware needs to resolve the JWT's `sub` claim to a
/// BFF user and check it is provisioned and active (section 8.2).</summary>
public sealed class AuthenticatedUserDto
{
    public Guid Id { get; init; }
    public string AuthUserId { get; init; } = string.Empty;
    public string FullName { get; init; } = string.Empty;
    public bool IsActive { get; init; }
}

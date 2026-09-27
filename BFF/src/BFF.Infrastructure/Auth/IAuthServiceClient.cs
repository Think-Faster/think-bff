namespace BFF.Infrastructure.Auth;

public interface IAuthServiceClient
{
    /// <summary>Requests a token refresh from the auth service, forwarding the incoming request's full
    /// Cookie header and returning every Set-Cookie header from the response verbatim (section 8.2).</summary>
    Task<RefreshResult> RefreshAsync(string cookieHeader, CancellationToken ct);
}

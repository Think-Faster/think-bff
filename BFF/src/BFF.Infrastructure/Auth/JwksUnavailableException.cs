namespace BFF.Infrastructure.Auth;

/// <summary>
/// Fetching or parsing the signing key from AUTH_JWKS_URL failed — unreachable, timed out, or the
/// response body isn't a key JwtValidator knows how to read. Deliberately distinct from a
/// token-validation failure: no token can be validated while this is happening, regardless of whether
/// the token itself is fine, so it isn't the caller's fault and shouldn't be reported as one (see
/// TokenAuthenticationMiddleware, which maps this to 503 `auth_service_unavailable` instead of a
/// token-related 401).
/// </summary>
public sealed class JwksUnavailableException : Exception
{
    public JwksUnavailableException(string message, Exception innerException) : base(message, innerException)
    {
    }
}

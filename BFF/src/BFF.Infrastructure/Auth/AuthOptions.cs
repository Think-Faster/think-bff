namespace BFF.Infrastructure.Auth;

public sealed class AuthOptions
{
    public const string SectionName = "Auth";

    public string ServiceUrl { get; set; } = string.Empty;
    public string RefreshPath { get; set; } = "/api/auth/refresh";
    public int RefreshTimeoutSeconds { get; set; } = 5;
    public string JwksUrl { get; set; } = string.Empty;
    public int JwksCacheMinutes { get; set; } = 60;
    public string Issuer { get; set; } = string.Empty;
    public string Audience { get; set; } = string.Empty;
    public string AccessTokenCookie { get; set; } = "access_token";
    public string UserIdClaim { get; set; } = "sub";
    public string[] SkipPaths { get; set; } = Array.Empty<string>();
}

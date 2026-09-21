namespace BFF.Infrastructure.Auth;

public sealed record RefreshResult(
    bool Success,
    IReadOnlyList<string> SetCookieHeaders,
    string? AccessToken);

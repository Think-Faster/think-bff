using System.Collections.Concurrent;
using System.Security.Claims;
using System.Text.Json;
using BFF.Application.Services;
using BFF.Contracts.Common;
using BFF.Infrastructure.Auth;
using BFF.WebApi.Extensions;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace BFF.WebApi.Middleware;

/// <summary>
/// The only place that parses the Authorization header / access-token cookie (section 8). Everything
/// downstream — controllers, the authorization handler — reads HttpContext.User /
/// HttpContext.GetCurrentUser() instead.
/// </summary>
public sealed class TokenAuthenticationMiddleware
{
    // Deduplicates concurrent refresh attempts for the same incoming cookie set, per section 8.2.
    private static readonly ConcurrentDictionary<string, Lazy<Task<RefreshResult>>> RefreshesInFlight = new();

    private readonly RequestDelegate _next;
    private readonly AuthOptions _options;
    private readonly ILogger<TokenAuthenticationMiddleware> _logger;

    public TokenAuthenticationMiddleware(RequestDelegate next, IOptions<AuthOptions> options, ILogger<TokenAuthenticationMiddleware> logger)
    {
        _next = next;
        _options = options.Value;
        _logger = logger;
    }

    public async Task InvokeAsync(
        HttpContext context,
        JwtValidator jwtValidator,
        IAuthServiceClient authServiceClient,
        IUserService userService)
    {
        if (IsSkipped(context.Request.Path))
        {
            await _next(context);
            return;
        }

        var ct = context.RequestAborted;
        var token = ExtractToken(context);

        if (token is null)
        {
            await WriteErrorAsync(context, StatusCodes.Status401Unauthorized, "unauthenticated", "No access token provided.");
            return;
        }

        ClaimsPrincipal principal;
        try
        {
            principal = await jwtValidator.ValidateAsync(token, ct);
        }
        catch (SecurityTokenExpiredException)
        {
            ClaimsPrincipal? refreshed;
            try
            {
                refreshed = await TryRefreshAsync(context, jwtValidator, authServiceClient, ct);
            }
            catch (JwksUnavailableException ex)
            {
                // Re-validating the refreshed token hit the same "no usable signing key" problem — same
                // 503 treatment as the outer catch below, just from inside this nested try.
                _logger.LogError(ex, "Cannot validate refreshed token: signing key unavailable from {JwksUrl}", _options.JwksUrl);
                await WriteErrorAsync(context, StatusCodes.Status503ServiceUnavailable, "auth_service_unavailable", "Cannot validate tokens right now.");
                return;
            }

            if (refreshed is null)
            {
                await WriteErrorAsync(context, StatusCodes.Status401Unauthorized, "token_refresh_failed", "Token refresh failed.");
                return;
            }

            principal = refreshed;
        }
        catch (JwksUnavailableException ex)
        {
            // Not the caller's fault: without a usable signing key, no token can be validated right now,
            // regardless of whether it's actually valid — that's a 503 on us, not a 401 on them.
            _logger.LogError(ex, "Cannot validate tokens: signing key unavailable from {JwksUrl}", _options.JwksUrl);
            await WriteErrorAsync(context, StatusCodes.Status503ServiceUnavailable, "auth_service_unavailable", "Cannot validate tokens right now.");
            return;
        }
        catch (SecurityTokenException)
        {
            await WriteErrorAsync(context, StatusCodes.Status401Unauthorized, "invalid_token", "The access token is malformed or its signature is invalid.");
            return;
        }

        var userIdClaim = principal.FindFirst(_options.UserIdClaim)?.Value;
        if (string.IsNullOrEmpty(userIdClaim))
        {
            await WriteErrorAsync(context, StatusCodes.Status401Unauthorized, "invalid_token", $"Token has no '{_options.UserIdClaim}' claim.");
            return;
        }

        var user = await userService.FindByAuthUserIdAsync(userIdClaim, ct);
        if (user is null)
        {
            await WriteErrorAsync(context, StatusCodes.Status403Forbidden, "user_not_provisioned", "The authenticated user is not provisioned in BFF.");
            return;
        }

        if (!user.IsActive)
        {
            await WriteErrorAsync(context, StatusCodes.Status403Forbidden, "user_inactive", "The authenticated user is deactivated.");
            return;
        }

        context.User = principal;
        context.SetCurrentUser(new CurrentUser(user.Id, user.AuthUserId, user.FullName));

        await _next(context);
    }

    /// <summary>Attempts a single refresh on SecurityTokenExpiredException. Returns the re-validated
    /// principal for the new access token, or null if the refresh or the re-validation failed.</summary>
    private static async Task<ClaimsPrincipal?> TryRefreshAsync(
        HttpContext context, JwtValidator jwtValidator, IAuthServiceClient authServiceClient, CancellationToken ct)
    {
        var cookieHeader = context.Request.Headers.Cookie.ToString();
        if (string.IsNullOrEmpty(cookieHeader))
        {
            return null;
        }

        var lazyRefresh = RefreshesInFlight.GetOrAdd(cookieHeader, key =>
            new Lazy<Task<RefreshResult>>(() => authServiceClient.RefreshAsync(key, ct)));

        RefreshResult result;
        try
        {
            result = await lazyRefresh.Value;
        }
        finally
        {
            RefreshesInFlight.TryRemove(cookieHeader, out _);
        }

        if (!result.Success)
        {
            return null;
        }

        foreach (var setCookie in result.SetCookieHeaders)
        {
            context.Response.Headers.Append("Set-Cookie", setCookie);
        }

        if (string.IsNullOrEmpty(result.AccessToken))
        {
            return null;
        }

        try
        {
            return await jwtValidator.ValidateAsync(result.AccessToken, ct);
        }
        catch (SecurityTokenException)
        {
            return null;
        }
    }

    private bool IsSkipped(PathString path)
    {
        var value = path.Value ?? string.Empty;
        return _options.SkipPaths.Any(skip =>
            skip.EndsWith('*')
                ? value.StartsWith(skip[..^1], StringComparison.OrdinalIgnoreCase)
                : string.Equals(value, skip, StringComparison.OrdinalIgnoreCase));
    }

    private string? ExtractToken(HttpContext context)
    {
        var header = context.Request.Headers.Authorization.ToString();
        if (!string.IsNullOrEmpty(header) &&
            header.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
        {
            return header["Bearer ".Length..].Trim();
        }

        return context.Request.Cookies.TryGetValue(_options.AccessTokenCookie, out var cookieValue)
            ? cookieValue
            : null;
    }

    private static async Task WriteErrorAsync(HttpContext context, int statusCode, string code, string message)
    {
        context.Response.StatusCode = statusCode;
        context.Response.ContentType = "application/json";
        await context.Response.WriteAsync(JsonSerializer.Serialize(new ErrorResponse { Code = code, Message = message }));
    }
}

using System.Text.Json;
using BFF.Contracts.Common;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authorization.Policy;

namespace BFF.WebApi.Authorization;

/// <summary>
/// Writes the spec's `{ code, message }` body directly on an authorization failure instead of going
/// through the default handler's Forbid/ChallengeAsync (which would need a registered authentication
/// scheme). This service never registers one: TokenAuthenticationMiddleware already owns 401s for
/// missing/invalid tokens and runs before the authorization middleware, so by the time this handler
/// sees a failed result it is always the "authenticated but lacks the permission" case (403), with the
/// bare-`[Authorize]` "must be authenticated" case (401) kept only as a defensive fallback.
/// </summary>
public sealed class PermissionDeniedResultHandler : IAuthorizationMiddlewareResultHandler
{
    public async Task HandleAsync(
        RequestDelegate next,
        HttpContext context,
        AuthorizationPolicy policy,
        PolicyAuthorizationResult authorizeResult)
    {
        if (authorizeResult.Succeeded)
        {
            await next(context);
            return;
        }

        var isAuthenticated = context.User.Identity?.IsAuthenticated == true;
        var statusCode = isAuthenticated ? StatusCodes.Status403Forbidden : StatusCodes.Status401Unauthorized;
        var code = isAuthenticated ? "permission_denied" : "unauthenticated";
        var message = isAuthenticated
            ? "You do not have the required permission for this operation."
            : "Authentication is required.";

        context.Response.StatusCode = statusCode;
        context.Response.ContentType = "application/json";
        await context.Response.WriteAsync(JsonSerializer.Serialize(new ErrorResponse { Code = code, Message = message }));
    }
}

namespace BFF.WebApi.Extensions;

/// <summary>The authenticated caller, resolved once by TokenAuthenticationMiddleware and stashed in
/// HttpContext.Items so controllers never re-parse claims themselves (section 8.2).</summary>
public sealed record CurrentUser(Guid UserId, string AuthUserId, string FullName);

public static class HttpContextExtensions
{
    private const string ItemsKey = "CurrentUser";

    public static CurrentUser? GetCurrentUser(this HttpContext context)
        => context.Items.TryGetValue(ItemsKey, out var value) ? value as CurrentUser : null;

    public static void SetCurrentUser(this HttpContext context, CurrentUser user)
        => context.Items[ItemsKey] = user;
}

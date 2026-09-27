using BFF.Infrastructure.Auth;
using BFF.WebApi.Authorization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authorization.Policy;
using Microsoft.Extensions.Options;

namespace BFF.WebApi.Extensions;

public static class ServiceCollectionExtensions
{
    private static readonly string[] RequiredEnvVars =
    {
        "DB_HOST", "DB_NAME", "DB_USER", "DB_PASSWORD", "DB_SCHEMA", "AUTH_SERVICE_URL", "AUTH_JWKS_URL",
    };

    public static void ValidateRequiredConfiguration(this IConfiguration configuration)
    {
        var missing = RequiredEnvVars.Where(key => string.IsNullOrEmpty(configuration[key])).ToList();
        if (missing.Count > 0)
        {
            throw new InvalidOperationException(
                $"Missing required configuration/environment variable(s): {string.Join(", ", missing)}. " +
                "The service refuses to start with silent defaults for these — see README.md.");
        }
    }

    public static IServiceCollection AddBffAuthentication(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<AuthOptions>(options =>
        {
            options.ServiceUrl = configuration["AUTH_SERVICE_URL"]!;
            options.RefreshPath = configuration["AUTH_REFRESH_PATH"] ?? "/api/auth/refresh";
            options.RefreshTimeoutSeconds = int.TryParse(configuration["AUTH_REFRESH_TIMEOUT_SECONDS"], out var t) ? t : 5;
            options.JwksUrl = configuration["AUTH_JWKS_URL"]!;
            options.JwksCacheMinutes = int.TryParse(configuration["AUTH_JWKS_CACHE_MINUTES"], out var m) ? m : 60;
            options.Issuer = configuration["AUTH_ISSUER"] ?? string.Empty;
            options.Audience = configuration["AUTH_AUDIENCE"] ?? string.Empty;
            options.AccessTokenCookie = configuration["AUTH_ACCESS_TOKEN_COOKIE"] ?? "access_token";
            options.UserIdClaim = configuration["AUTH_USER_ID_CLAIM"] ?? "sub";
            options.SkipPaths = configuration.GetSection("Auth:SkipPaths").Get<string[]>()
                ?? new[] { "/health/live", "/health/ready" };
        });

        services.AddHttpClient<IAuthServiceClient, AuthServiceClient>((provider, client) =>
        {
            var options = provider.GetRequiredService<IOptions<AuthOptions>>().Value;
            client.BaseAddress = new Uri(options.ServiceUrl);
        }).ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler
        {
            // The auth service reads/writes cookies itself; letting HttpClientHandler manage its own
            // cookie jar would swallow the incoming Cookie header and the response's Set-Cookie headers
            // instead of passing them through verbatim (section 8.2).
            UseCookies = false,
        });

        services.AddHttpClient(nameof(JwtValidator));
        services.AddSingleton<JwtValidator>();

        return services;
    }

    public static IServiceCollection AddBffAuthorization(this IServiceCollection services)
    {
        services.AddHttpContextAccessor();
        services.AddSingleton<IAuthorizationPolicyProvider, PermissionPolicyProvider>();
        services.AddScoped<IAuthorizationHandler, PermissionAuthorizationHandler>();
        services.AddSingleton<IAuthorizationMiddlewareResultHandler, PermissionDeniedResultHandler>();
        services.AddAuthorization();

        return services;
    }

    public static IServiceCollection AddBffCors(this IServiceCollection services, IConfiguration configuration)
    {
        var origins = (configuration["CORS_ALLOWED_ORIGINS"] ?? string.Empty)
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        services.AddCors(options => options.AddDefaultPolicy(policy =>
        {
            if (origins.Length > 0)
            {
                policy.WithOrigins(origins).AllowAnyHeader().AllowAnyMethod().AllowCredentials();
            }
        }));

        return services;
    }
}

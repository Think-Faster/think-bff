using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace BFF.Infrastructure.Auth;

/// <summary>Local JWT validation against JWKS keys fetched from the auth service and cached in memory
/// (section 8.2). Does not call the auth service for anything other than fetching keys.</summary>
public sealed class JwtValidator
{
    private const string JwksCacheKey = "bff:jwks";

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly AuthOptions _options;
    private readonly IMemoryCache _cache;

    public JwtValidator(IHttpClientFactory httpClientFactory, IOptions<AuthOptions> options, IMemoryCache cache)
    {
        _httpClientFactory = httpClientFactory;
        _options = options.Value;
        _cache = cache;
    }

    /// <summary>Validates signature, issuer, audience and lifetime. Throws
    /// <see cref="SecurityTokenExpiredException"/> when only the lifetime check failed (the middleware's
    /// signal to attempt a refresh), or another <see cref="SecurityTokenException"/> for anything else.</summary>
    public async Task<ClaimsPrincipal> ValidateAsync(string token, CancellationToken ct)
    {
        var handler = new JwtSecurityTokenHandler();
        var parameters = BuildValidationParameters(await GetSigningKeysAsync(forceRefresh: false, ct));

        try
        {
            return handler.ValidateToken(token, parameters, out _);
        }
        catch (SecurityTokenSignatureKeyNotFoundException)
        {
            // Unknown kid: the auth service may have rotated its keys — refresh once and retry.
            parameters = BuildValidationParameters(await GetSigningKeysAsync(forceRefresh: true, ct));
            return handler.ValidateToken(token, parameters, out _);
        }
    }

    private TokenValidationParameters BuildValidationParameters(IReadOnlyList<SecurityKey> signingKeys) => new()
    {
        ValidateIssuer = true,
        ValidIssuer = _options.Issuer,
        ValidateAudience = true,
        ValidAudience = _options.Audience,
        ValidateLifetime = true,
        ValidateIssuerSigningKey = true,
        IssuerSigningKeys = signingKeys,
        ClockSkew = TimeSpan.FromSeconds(30),
    };

    private async Task<IReadOnlyList<SecurityKey>> GetSigningKeysAsync(bool forceRefresh, CancellationToken ct)
    {
        if (!forceRefresh && _cache.TryGetValue(JwksCacheKey, out IReadOnlyList<SecurityKey>? cached) && cached is not null)
        {
            return cached;
        }

        var client = _httpClientFactory.CreateClient(nameof(JwtValidator));
        var json = await client.GetStringAsync(_options.JwksUrl, ct);
        var keySet = new JsonWebKeySet(json);
        IReadOnlyList<SecurityKey> keys = keySet.GetSigningKeys().ToList();

        _cache.Set(JwksCacheKey, keys, TimeSpan.FromMinutes(_options.JwksCacheMinutes));
        return keys;
    }
}

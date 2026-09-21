using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace BFF.Infrastructure.Auth;

/// <summary>Local JWT validation against the signing key fetched from the auth service and cached in
/// memory (section 8.2). Does not call the auth service for anything other than fetching that key.</summary>
public sealed class JwtValidator
{
    private const string SigningKeysCacheKey = "bff:jwt-signing-keys";

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
    /// signal to attempt a refresh), <see cref="JwksUnavailableException"/> if the signing key itself
    /// couldn't be obtained, or another <see cref="SecurityTokenException"/> for anything else.</summary>
    public async Task<ClaimsPrincipal> ValidateAsync(string token, CancellationToken ct)
    {
        var handler = new JwtSecurityTokenHandler
        {
            // Without this, JwtSecurityTokenHandler silently renames well-known short claim types (most
            // notably "sub") to legacy XML/SOAP URIs via its DefaultInboundClaimTypeMap — so the resulting
            // ClaimsPrincipal never has a claim literally named "sub", breaking AUTH_USER_ID_CLAIM lookups
            // even though the raw JWT payload clearly has one. Keep claim names exactly as issued.
            MapInboundClaims = false,
        };
        var parameters = BuildValidationParameters(await GetSigningKeysAsync(forceRefresh: false, ct));

        try
        {
            return handler.ValidateToken(token, parameters, out _);
        }
        catch (SecurityTokenSignatureKeyNotFoundException)
        {
            // No cached key matched: the auth service may have rotated its key — refresh once and retry.
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
        if (!forceRefresh && _cache.TryGetValue(SigningKeysCacheKey, out IReadOnlyList<SecurityKey>? cached) && cached is not null)
        {
            return cached;
        }

        string body;
        try
        {
            var client = _httpClientFactory.CreateClient(nameof(JwtValidator));
            body = await client.GetStringAsync(_options.JwksUrl, ct);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            throw new JwksUnavailableException($"Could not fetch the signing key from '{_options.JwksUrl}'.", ex);
        }

        IReadOnlyList<SecurityKey> keys;
        try
        {
            keys = ParseSigningKeys(body);
        }
        catch (Exception ex)
        {
            throw new JwksUnavailableException(
                $"'{_options.JwksUrl}' returned a body JwtValidator could not parse as a JWKS document or a PEM public key.", ex);
        }

        _cache.Set(SigningKeysCacheKey, keys, TimeSpan.FromMinutes(_options.JwksCacheMinutes));
        return keys;
    }

    /// <summary>
    /// AUTH_JWKS_URL is named after the standard JWKS ("keys": [...]) document format, but this auth
    /// service currently returns a bare PEM-encoded public key instead (no wrapping JSON, no "kid") — see
    /// docs/DECISIONS.md. Handling both here means a future fix on the auth side (switching to a real
    /// JWKS document) keeps working without a BFF code change.
    /// </summary>
    private static IReadOnlyList<SecurityKey> ParseSigningKeys(string body)
    {
        var trimmed = body.Trim();

        if (trimmed.StartsWith('{'))
        {
            var keySet = new JsonWebKeySet(trimmed);
            return keySet.GetSigningKeys().ToList();
        }

        return new[] { ParsePemPublicKey(trimmed) };
    }

    private static SecurityKey ParsePemPublicKey(string pem)
    {
        try
        {
            var rsa = RSA.Create();
            rsa.ImportFromPem(pem);
            return new RsaSecurityKey(rsa);
        }
        catch (CryptographicException)
        {
            var ecdsa = ECDsa.Create();
            ecdsa.ImportFromPem(pem);
            return new ECDsaSecurityKey(ecdsa);
        }
    }
}

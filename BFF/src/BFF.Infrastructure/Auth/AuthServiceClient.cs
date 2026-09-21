using System.Net;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace BFF.Infrastructure.Auth;

public sealed class AuthServiceClient : IAuthServiceClient
{
    private readonly HttpClient _httpClient;
    private readonly AuthOptions _options;
    private readonly ILogger<AuthServiceClient> _logger;

    public AuthServiceClient(HttpClient httpClient, IOptions<AuthOptions> options, ILogger<AuthServiceClient> logger)
    {
        _httpClient = httpClient;
        _options = options.Value;
        _logger = logger;
    }

    public async Task<RefreshResult> RefreshAsync(string cookieHeader, CancellationToken ct)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, _options.RefreshPath);
        request.Headers.TryAddWithoutValidation("Cookie", cookieHeader);

        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeoutCts.CancelAfter(TimeSpan.FromSeconds(_options.RefreshTimeoutSeconds));

        HttpResponseMessage response;
        try
        {
            response = await _httpClient.SendAsync(request, timeoutCts.Token);
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            _logger.LogWarning("Token refresh request timed out after {Timeout}s", _options.RefreshTimeoutSeconds);
            return new RefreshResult(false, Array.Empty<string>(), null);
        }
        catch (HttpRequestException ex)
        {
            _logger.LogWarning(ex, "Token refresh request failed");
            return new RefreshResult(false, Array.Empty<string>(), null);
        }

        using (response)
        {
            if (response.StatusCode is not (HttpStatusCode.OK or HttpStatusCode.NoContent))
            {
                return new RefreshResult(false, Array.Empty<string>(), null);
            }

            var setCookieHeaders = response.Headers.TryGetValues("Set-Cookie", out var values)
                ? values.ToArray()
                : Array.Empty<string>();

            var accessToken = ExtractAccessTokenFromCookies(setCookieHeaders)
                ?? await ExtractAccessTokenFromBodyAsync(response, ct);

            return new RefreshResult(true, setCookieHeaders, accessToken);
        }
    }

    private string? ExtractAccessTokenFromCookies(IReadOnlyList<string> setCookieHeaders)
    {
        var prefix = _options.AccessTokenCookie + "=";
        foreach (var header in setCookieHeaders)
        {
            var firstSegment = header.Split(';', 2)[0].Trim();
            if (firstSegment.StartsWith(prefix, StringComparison.Ordinal))
            {
                return Uri.UnescapeDataString(firstSegment[prefix.Length..]);
            }
        }

        return null;
    }

    private static async Task<string?> ExtractAccessTokenFromBodyAsync(HttpResponseMessage response, CancellationToken ct)
    {
        if (response.Content.Headers.ContentLength is 0)
        {
            return null;
        }

        try
        {
            await using var stream = await response.Content.ReadAsStreamAsync(ct);
            using var document = await JsonDocument.ParseAsync(stream, cancellationToken: ct);

            if (document.RootElement.ValueKind != JsonValueKind.Object)
            {
                return null;
            }

            if (document.RootElement.TryGetProperty("accessToken", out var camelCase))
            {
                return camelCase.GetString();
            }

            if (document.RootElement.TryGetProperty("access_token", out var snakeCase))
            {
                return snakeCase.GetString();
            }
        }
        catch (JsonException)
        {
            // No JSON body (or not the shape we expect) — the new token, if any, came from Set-Cookie.
        }

        return null;
    }
}

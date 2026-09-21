using BFF.Application.Services;
using BFF.Infrastructure.Auth;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace BFF.WebApi.Controllers;

[ApiController]
[Route("health")]
public sealed class HealthController : ControllerBase
{
    private readonly IHealthService _healthService;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly AuthOptions _authOptions;
    private readonly ILogger<HealthController> _logger;

    public HealthController(
        IHealthService healthService,
        IHttpClientFactory httpClientFactory,
        IOptions<AuthOptions> authOptions,
        ILogger<HealthController> logger)
    {
        _healthService = healthService;
        _httpClientFactory = httpClientFactory;
        _authOptions = authOptions.Value;
        _logger = logger;
    }

    /// <summary>Process liveness only — never touches the database (section 10.4).</summary>
    [HttpGet("live")]
    public IActionResult Live() => Ok(new { status = "ok" });

    [HttpGet("ready")]
    public async Task<IActionResult> Ready(CancellationToken ct)
    {
        var databaseReady = await _healthService.IsDatabaseReadyAsync(ct);
        var jwksReady = await IsJwksReachableAsync(ct);

        var body = new { status = databaseReady && jwksReady ? "ok" : "unavailable", database = databaseReady, jwks = jwksReady };
        return databaseReady && jwksReady ? Ok(body) : StatusCode(StatusCodes.Status503ServiceUnavailable, body);
    }

    private async Task<bool> IsJwksReachableAsync(CancellationToken ct)
    {
        try
        {
            var client = _httpClientFactory.CreateClient(nameof(HealthController));
            using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            timeoutCts.CancelAfter(TimeSpan.FromSeconds(3));
            var response = await client.GetAsync(_authOptions.JwksUrl, timeoutCts.Token);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning(
                    "JWKS health check got a non-success status from {JwksUrl}: {StatusCode}",
                    _authOptions.JwksUrl, (int)response.StatusCode);
            }

            return response.IsSuccessStatusCode;
        }
        catch (Exception ex) when (!ct.IsCancellationRequested)
        {
            // Swallowed on purpose — this only feeds /health/ready's boolean, never rethrown — but logged
            // so "jwks: false" doesn't require guessing whether it's DNS, connection refused, TLS, or a
            // timeout against AUTH_JWKS_URL.
            _logger.LogWarning(ex, "JWKS health check failed against {JwksUrl}", _authOptions.JwksUrl);
            return false;
        }
    }
}

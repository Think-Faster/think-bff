using BFF.Application.Services;
using BFF.WebApi.Extensions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BFF.WebApi.Controllers;

/// <summary>Sensor readings are not stored in BFF — tf-funnel archives and streams them (the «Логи» window).
/// BFF only answers whose readings a user may see.</summary>
[ApiController]
[Authorize]
[Route("readings")]
public sealed class ReadingsController : ControllerBase
{
    private const int MaxObjects = 100;

    private readonly IReadingsScopeService _scopeService;

    public ReadingsController(IReadingsScopeService scopeService)
    {
        _scopeService = scopeService;
    }

    /// <summary>No [RequirePermission]: engineers have no <c>readings</c> grant, their scope comes from their
    /// tasks. The service decides — see <see cref="ReadingsScopeService"/>.</summary>
    [HttpGet("scope")]
    public async Task<IActionResult> Scope([FromQuery] int[]? objectId, CancellationToken ct)
    {
        var ids = objectId ?? Array.Empty<int>();
        if (ids.Length > MaxObjects || ids.Any(id => id <= 0))
        {
            throw new ArgumentException($"objectId: up to {MaxObjects} positive ids.", nameof(objectId));
        }

        var currentUser = HttpContext.GetCurrentUser()!;
        return Ok(await _scopeService.GetScopeAsync(currentUser.UserId, ids, ct));
    }
}

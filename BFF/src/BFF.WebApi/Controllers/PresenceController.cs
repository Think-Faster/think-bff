using BFF.Application.Services;
using BFF.Models.Constants;
using BFF.Models.Enums;
using BFF.WebApi.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BFF.WebApi.Controllers;

[ApiController]
[Route("presence")]
public sealed class PresenceController : ControllerBase
{
    private readonly IPresenceTracker _presenceTracker;

    public PresenceController(IPresenceTracker presenceTracker)
    {
        _presenceTracker = presenceTracker;
    }

    [HttpGet]
    [RequirePermission(ResourceCodes.Presence, PermissionFlags.Read)]
    public async Task<IActionResult> List([FromQuery] Guid[]? userIds, CancellationToken ct)
        => Ok(await _presenceTracker.ListAsync(userIds, ct));
}

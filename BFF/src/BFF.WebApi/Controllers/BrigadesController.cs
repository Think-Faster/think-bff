using BFF.Application.Services;
using BFF.Contracts.Engineers;
using BFF.Models.Constants;
using BFF.Models.Enums;
using BFF.WebApi.Authorization;
using FluentValidation;
using Microsoft.AspNetCore.Mvc;

namespace BFF.WebApi.Controllers;

[ApiController]
[Route("brigades")]
public sealed class BrigadesController : ControllerBase
{
    private readonly IEngineerService _engineerService;
    private readonly IValidator<CreateBrigadeRequest> _createValidator;

    public BrigadesController(IEngineerService engineerService, IValidator<CreateBrigadeRequest> createValidator)
    {
        _engineerService = engineerService;
        _createValidator = createValidator;
    }

    [HttpGet]
    [RequirePermission(ResourceCodes.Engineers, PermissionFlags.Read)]
    public async Task<IActionResult> List(CancellationToken ct)
        => Ok(await _engineerService.ListBrigadesAsync(ct));

    [HttpPost]
    [RequirePermission(ResourceCodes.Engineers, PermissionFlags.Create)]
    public async Task<IActionResult> Create([FromBody] CreateBrigadeRequest request, CancellationToken ct)
    {
        await _createValidator.ValidateAndThrowAsync(request, ct);
        var result = await _engineerService.CreateBrigadeAsync(request, ct);
        return StatusCode(StatusCodes.Status201Created, result);
    }
}

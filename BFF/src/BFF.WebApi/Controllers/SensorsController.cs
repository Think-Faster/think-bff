using BFF.Application.Services;
using BFF.Contracts.Sensors;
using BFF.Models.Constants;
using BFF.Models.Enums;
using BFF.WebApi.Authorization;
using FluentValidation;
using Microsoft.AspNetCore.Mvc;

namespace BFF.WebApi.Controllers;

[ApiController]
[Route("sensors")]
public sealed class SensorsController : ControllerBase
{
    private readonly ISensorService _sensorService;
    private readonly IValidator<CreateSensorRequest> _createValidator;
    private readonly IValidator<UpdateSensorRequest> _updateValidator;
    private readonly IValidator<CreateSensorLinkRequest> _createLinkValidator;

    public SensorsController(
        ISensorService sensorService,
        IValidator<CreateSensorRequest> createValidator,
        IValidator<UpdateSensorRequest> updateValidator,
        IValidator<CreateSensorLinkRequest> createLinkValidator)
    {
        _sensorService = sensorService;
        _createValidator = createValidator;
        _updateValidator = updateValidator;
        _createLinkValidator = createLinkValidator;
    }

    [HttpGet]
    [RequirePermission(ResourceCodes.Sensors, PermissionFlags.Read)]
    public async Task<IActionResult> List(
        [FromQuery] int? objectId, [FromQuery] string? search,
        [FromQuery] int page = 1, [FromQuery] int pageSize = 50, CancellationToken ct = default)
        => Ok(await _sensorService.ListAsync(objectId, search, page, pageSize, ct));

    /// <summary>Подсистемы и типы, которые уже встречаются у датчиков, — варианты выбора в форме.</summary>
    [HttpGet("types")]
    [RequirePermission(ResourceCodes.Sensors, PermissionFlags.Read)]
    public async Task<IActionResult> Types(CancellationToken ct)
        => Ok(await _sensorService.ListTypesAsync(ct));

    [HttpGet("{id:int}")]
    [RequirePermission(ResourceCodes.Sensors, PermissionFlags.Read)]
    public async Task<IActionResult> Get(int id, CancellationToken ct)
        => Ok(await _sensorService.GetAsync(id, ct));

    [HttpPost]
    [RequirePermission(ResourceCodes.Sensors, PermissionFlags.Create)]
    public async Task<IActionResult> Create([FromBody] CreateSensorRequest request, CancellationToken ct)
    {
        await _createValidator.ValidateAndThrowAsync(request, ct);
        var result = await _sensorService.CreateAsync(request, ct);
        return CreatedAtAction(nameof(Get), new { id = result.Id }, result);
    }

    [HttpPut("{id:int}")]
    [RequirePermission(ResourceCodes.Sensors, PermissionFlags.Update)]
    public async Task<IActionResult> Update(int id, [FromBody] UpdateSensorRequest request, CancellationToken ct)
    {
        await _updateValidator.ValidateAndThrowAsync(request, ct);
        return Ok(await _sensorService.UpdateAsync(id, request, ct));
    }

    [HttpDelete("{id:int}")]
    [RequirePermission(ResourceCodes.Sensors, PermissionFlags.Delete)]
    public async Task<IActionResult> Delete(int id, CancellationToken ct)
    {
        await _sensorService.DeleteAsync(id, ct);
        return NoContent();
    }

    [HttpGet("{id:int}/links")]
    [RequirePermission(ResourceCodes.Sensors, PermissionFlags.Read)]
    public async Task<IActionResult> ListLinks(int id, CancellationToken ct)
        => Ok(await _sensorService.ListLinksAsync(id, ct));

    [HttpPost("{id:int}/links")]
    [RequirePermission(ResourceCodes.Sensors, PermissionFlags.Update)]
    public async Task<IActionResult> CreateLink(int id, [FromBody] CreateSensorLinkRequest request, CancellationToken ct)
    {
        await _createLinkValidator.ValidateAndThrowAsync(request, ct);
        var result = await _sensorService.CreateLinkAsync(id, request, ct);
        return CreatedAtAction(nameof(ListLinks), new { id }, result);
    }

    [HttpDelete("{id:int}/links/{toSensorId:int}/{kind}")]
    [RequirePermission(ResourceCodes.Sensors, PermissionFlags.Update)]
    public async Task<IActionResult> DeleteLink(int id, int toSensorId, string kind, CancellationToken ct)
    {
        await _sensorService.DeleteLinkAsync(id, toSensorId, kind, ct);
        return NoContent();
    }
}

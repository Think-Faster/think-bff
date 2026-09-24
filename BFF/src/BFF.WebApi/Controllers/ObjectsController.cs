using BFF.Application.Services;
using BFF.Contracts.Objects;
using BFF.Models.Constants;
using BFF.Models.Enums;
using BFF.WebApi.Authorization;
using FluentValidation;
using Microsoft.AspNetCore.Mvc;

namespace BFF.WebApi.Controllers;

[ApiController]
[Route("objects")]
public sealed class ObjectsController : ControllerBase
{
    private readonly IObjectService _objectService;
    private readonly IValidator<CreateObjectRequest> _createValidator;
    private readonly IValidator<UpdateObjectRequest> _updateValidator;
    private readonly IValidator<CreatePicketRequest> _createPicketValidator;
    private readonly IValidator<UpdatePicketRequest> _updatePicketValidator;
    private readonly IValidator<UpsertMapLayerRequest> _upsertLayerValidator;

    public ObjectsController(
        IObjectService objectService,
        IValidator<CreateObjectRequest> createValidator,
        IValidator<UpdateObjectRequest> updateValidator,
        IValidator<CreatePicketRequest> createPicketValidator,
        IValidator<UpdatePicketRequest> updatePicketValidator,
        IValidator<UpsertMapLayerRequest> upsertLayerValidator)
    {
        _objectService = objectService;
        _createValidator = createValidator;
        _updateValidator = updateValidator;
        _createPicketValidator = createPicketValidator;
        _updatePicketValidator = updatePicketValidator;
        _upsertLayerValidator = upsertLayerValidator;
    }

    [HttpGet]
    [RequirePermission(ResourceCodes.Objects, PermissionFlags.Read)]
    public async Task<IActionResult> List([FromQuery] string? search, [FromQuery] int page = 1, [FromQuery] int pageSize = 50, CancellationToken ct = default)
    {
        return Ok(await _objectService.ListAsync(search, page, pageSize, ct));
    }

    [HttpGet("{id:int}")]
    [RequirePermission(ResourceCodes.Objects, PermissionFlags.Read)]
    public async Task<IActionResult> Get(int id, CancellationToken ct)
        => Ok(await _objectService.GetAsync(id, ct));

    [HttpPost]
    [RequirePermission(ResourceCodes.Objects, PermissionFlags.Create)]
    public async Task<IActionResult> Create([FromBody] CreateObjectRequest request, CancellationToken ct)
    {
        await _createValidator.ValidateAndThrowAsync(request, ct);
        var result = await _objectService.CreateAsync(request, ct);
        return CreatedAtAction(nameof(Get), new { id = result.Id }, result);
    }

    [HttpPut("{id:int}")]
    [RequirePermission(ResourceCodes.Objects, PermissionFlags.Update)]
    public async Task<IActionResult> Update(int id, [FromBody] UpdateObjectRequest request, CancellationToken ct)
    {
        await _updateValidator.ValidateAndThrowAsync(request, ct);
        return Ok(await _objectService.UpdateAsync(id, request, ct));
    }

    [HttpDelete("{id:int}")]
    [RequirePermission(ResourceCodes.Objects, PermissionFlags.Delete)]
    public async Task<IActionResult> Delete(int id, CancellationToken ct)
    {
        await _objectService.DeleteAsync(id, ct);
        return NoContent();
    }

    [HttpGet("{id:int}/pickets")]
    [RequirePermission(ResourceCodes.Objects, PermissionFlags.Read)]
    public async Task<IActionResult> ListPickets(int id, CancellationToken ct)
        => Ok(await _objectService.ListPicketsAsync(id, ct));

    [HttpPost("{id:int}/pickets")]
    [RequirePermission(ResourceCodes.Objects, PermissionFlags.Update)]
    public async Task<IActionResult> CreatePicket(int id, [FromBody] CreatePicketRequest request, CancellationToken ct)
    {
        await _createPicketValidator.ValidateAndThrowAsync(request, ct);
        var result = await _objectService.CreatePicketAsync(id, request, ct);
        return CreatedAtAction(nameof(ListPickets), new { id }, result);
    }

    [HttpPut("{id:int}/pickets/{picketId:long}")]
    [RequirePermission(ResourceCodes.Objects, PermissionFlags.Update)]
    public async Task<IActionResult> UpdatePicket(int id, long picketId, [FromBody] UpdatePicketRequest request, CancellationToken ct)
    {
        await _updatePicketValidator.ValidateAndThrowAsync(request, ct);
        return Ok(await _objectService.UpdatePicketAsync(id, picketId, request, ct));
    }

    [HttpDelete("{id:int}/pickets/{picketId:long}")]
    [RequirePermission(ResourceCodes.Objects, PermissionFlags.Update)]
    public async Task<IActionResult> DeletePicket(int id, long picketId, CancellationToken ct)
    {
        await _objectService.DeletePicketAsync(id, picketId, ct);
        return NoContent();
    }

    [HttpGet("{id:int}/layers")]
    [RequirePermission(ResourceCodes.Objects, PermissionFlags.Read)]
    public async Task<IActionResult> ListLayers(int id, [FromQuery] short? level, CancellationToken ct)
        => Ok(await _objectService.ListLayersAsync(id, level, ct));

    [HttpPost("{id:int}/layers")]
    [RequirePermission(ResourceCodes.Objects, PermissionFlags.Update)]
    public async Task<IActionResult> UpsertLayer(int id, [FromBody] UpsertMapLayerRequest request, CancellationToken ct)
    {
        await _upsertLayerValidator.ValidateAndThrowAsync(request, ct);
        return Ok(await _objectService.UpsertLayerAsync(id, request, ct));
    }
}

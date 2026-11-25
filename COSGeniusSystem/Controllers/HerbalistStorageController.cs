using System.ComponentModel.DataAnnotations;
using COSGeniusSystem.Commons;
using COSGeniusSystem.Services.Herbalist;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace COSGeniusSystem.Controllers;

[HerbalistConflict]
[ApiController]
[Route("api/v1/warehouse/storage-locations")]
[Authorize(Roles = "ADMIN,INVENTORY_STAFF")]
public class HerbalistStorageController(IHerbalistStorageService service) : ControllerBase
{
    public record CreateLocation([property: Range(1, int.MaxValue)] int WarehouseId, [property: Required, StringLength(100, MinimumLength = 1)] string Code);
    public record AssignLocation([property: Range(1, int.MaxValue)] int StorageLocationId);
    private IActionResult Result(HerbalistStorageResult result)
    {
        var status = result.Outcome switch { HerbalistStorageOutcome.Success => 200, HerbalistStorageOutcome.Created => 201, HerbalistStorageOutcome.Invalid => 400, HerbalistStorageOutcome.NotFound => 404, HerbalistStorageOutcome.Conflict => 409, _ => throw new InvalidOperationException("Unknown storage outcome.") };
        return StatusCode(status, new ApiResponseModel<object>(status, result.Message, status < 300, result.Data));
    }

    [HttpGet]
    public async Task<IActionResult> List([FromQuery, Range(1, int.MaxValue)] int warehouseId, CancellationToken ct) => Result(await service.ListAsync(warehouseId, ct));

    [HttpPost]
    public async Task<IActionResult> Create(CreateLocation input, CancellationToken ct) => Result(await service.CreateAsync(input.WarehouseId, input.Code, ct));

    [HttpPut("batches/{batchId:int}")]
    public async Task<IActionResult> Assign(int batchId, AssignLocation input, CancellationToken ct) => Result(await service.AssignAsync(batchId, input.StorageLocationId, ct));
}

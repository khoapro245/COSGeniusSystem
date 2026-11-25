using COSGeniusSystem.Services.Herbalist;
using System.ComponentModel.DataAnnotations;
using System.Data;
using System.Security.Claims;
using COSGeniusSystem.Commons;
using COSGeniusSystem.Data;
using COSGeniusSystem.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace COSGeniusSystem.Controllers;

// Warehouse bridge for the grouped requests. Never exposed as an herbalist action.
[COSGeniusSystem.Services.Herbalist.HerbalistConflict]
[ApiController]
[Route("api/v1/warehouse/herbalist-requests")]
[Authorize(Roles = "ADMIN,INVENTORY_STAFF")]
public class HerbalistWarehouseController(IHerbalistWarehouseQueryService service, IHerbalistWarehouseService warehouseService) : ControllerBase
{
    public record AllocationInput([param: Range(1, int.MaxValue)] int BatchId, [param: Range(typeof(decimal), "0.001", "9999999999")] decimal QuantityGrams);
    public record IssueInput([param: Required] string RowVersion, [param: Required, MinLength(1), MaxLength(1000)] AllocationInput[] Allocations);
    public record RejectInput([param: Required] string RowVersion, [param: Required, StringLength(1000, MinimumLength = 1)] string Reason);
    public record ResolveInput([param: Required] string RowVersion, [param: Required, StringLength(2000, MinimumLength = 1)] string Resolution, bool PhysicalCorrectionConfirmed);
    private IActionResult Result(int status, string message, object? data = null) => StatusCode(status, new ApiResponseModel<object>(status, message, status < 300, data));
    private int Actor => int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
    [HttpGet]
    public async Task<IActionResult> List([FromQuery, Range(1, int.MaxValue)] int pageNumber = 1, [FromQuery] string? status = null, CancellationToken ct = default)
    {
        var result = await service.ListAsync(pageNumber, status, ct);
        return MutationResponse(new WarehouseMutationResult(result.Outcome, result.Message, result.Data));
    }


    [HttpGet("{id:int}")]
    public async Task<IActionResult> Detail(int id, CancellationToken ct)
    {
        var result = await service.DetailAsync(id, ct);
        return MutationResponse(new WarehouseMutationResult(result.Outcome, result.Message, result.Data));
    }

    [HttpGet("{id:int}/batches")]
    public async Task<IActionResult> EligibleBatches(int id, CancellationToken ct)
    {
        var result = await service.EligibleBatchesAsync(id, ct);
        return MutationResponse(new WarehouseMutationResult(result.Outcome, result.Message, result.Data));
    }

    private IActionResult MutationResponse(WarehouseMutationResult result) => Result(result.Outcome switch
    {
        WarehouseMutationOutcome.Success => 200, WarehouseMutationOutcome.Invalid => 400,
        WarehouseMutationOutcome.NotFound => 404, WarehouseMutationOutcome.Conflict => 409,
        WarehouseMutationOutcome.Unprocessable => 422, WarehouseMutationOutcome.SchemaUnavailable => 503,
        _ => throw new InvalidOperationException("Unknown warehouse outcome.")
    }, result.Message, result.Data);

    [HttpPost("{id:int}/issue")]
    public async Task<IActionResult> Issue(int id, IssueInput input, CancellationToken ct) =>
        MutationResponse(await warehouseService.IssueAsync(id, input, Actor, ct));

    [HttpPost("{id:int}/resolve-discrepancy")]
    public async Task<IActionResult> ResolveDiscrepancy(int id, ResolveInput input, CancellationToken ct) =>
        MutationResponse(await warehouseService.ResolveDiscrepancyAsync(id, input, Actor, ct));

    [HttpPost("{id:int}/reject")]
    public async Task<IActionResult> Reject(int id, RejectInput input, CancellationToken ct) =>
        MutationResponse(await warehouseService.RejectAsync(id, input, Actor, ct));

    [HttpGet("{id:int}/fefo")]
    public async Task<IActionResult> Fefo(int id,CancellationToken ct)
    {
        var result = await service.FefoAsync(id, ct);
        return MutationResponse(new WarehouseMutationResult(result.Outcome, result.Message, result.Data));
    }
}




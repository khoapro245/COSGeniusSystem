using COSGeniusSystem.Commons;
using COSGeniusSystem.Data;
using COSGeniusSystem.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace COSGeniusSystem.Controllers;

public class WarehouseResponseDto
{
    public int WarehouseId { get; set; }
    public string WarehouseCode { get; set; } = string.Empty;
    public string WarehouseName { get; set; } = string.Empty;
    public string? Location { get; set; }
    public bool IsActive { get; set; }
}

[ApiController]
[Route("api/v1/warehouses")]
public class WarehousesController : BaseApiController
{
    private readonly ApplicationDbContext _context;

    public WarehousesController(ApplicationDbContext context)
    {
        _context = context;
    }

    /// <summary>
    /// API-WH-01: Lấy danh sách các kho lưu trữ từ Database.
    /// GET /api/v1/warehouses
    /// </summary>
    [HttpGet]
    [AllowAnonymous]
    public async Task<ActionResult<ApiResponseModel<List<WarehouseResponseDto>>>> GetWarehouses(CancellationToken ct = default)
    {
        var list = await _context.Warehouses
            .AsNoTracking()
            .Where(w => w.IsActive)
            .OrderBy(w => w.WarehouseId)
            .Select(w => new WarehouseResponseDto
            {
                WarehouseId = w.WarehouseId,
                WarehouseCode = w.WarehouseCode,
                WarehouseName = w.WarehouseName,
                Location = w.Location,
                IsActive = w.IsActive
            })
            .ToListAsync(ct);

        return SuccessResponse(new SystemMessage(200, "Warehouses retrieved successfully.", true), list);
    }
}


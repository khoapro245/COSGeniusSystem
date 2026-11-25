using COSGeniusSystem.Commons;
using COSGeniusSystem.DTOs;
using COSGeniusSystem.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace COSGeniusSystem.Controllers;

[ApiController]
[Route("api/v1/warehouses")]
public class WarehousesController : BaseApiController
{
    private readonly IWarehouseService _service;

    public WarehousesController(IWarehouseService service)
    {
        _service = service;
    }

    /// <summary>
    /// API-WH-01: Lấy danh sách các kho lưu trữ từ Database.
    /// GET /api/v1/warehouses
    /// </summary>
    [HttpGet]
    [AllowAnonymous]
    public async Task<ActionResult<ApiResponseModel<List<WarehouseResponseDto>>>> GetWarehouses(CancellationToken ct = default)
    {
        var list = await _service.GetWarehousesAsync(ct);

        return SuccessResponse(new SystemMessage(200, "Warehouses retrieved successfully.", true), list);
    }
}


using COSGeniusSystem.Commons;
using COSGeniusSystem.DTOs.Suppliers;
using COSGeniusSystem.Exceptions;
using COSGeniusSystem.Services.Suppliers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace COSGeniusSystem.Controllers;

[ApiController]
[Route("api/v1/suppliers")]
public class SuppliersController : BaseApiController
{
    private readonly ISupplierService _supplierService;

    public SuppliersController(ISupplierService supplierService)
    {
        _supplierService = supplierService;
    }

    /// <summary>
    /// API-NCC-01: Lấy danh sách nhà cung cấp, hỗ trợ tìm kiếm và phân trang.
    /// GET /api/v1/suppliers
    /// </summary>
    [HttpGet]
    [Authorize]
    public async Task<ActionResult<ApiResponseModel<List<SupplierResponseDto>>>> GetSuppliers([FromQuery] SupplierFilterQuery query, CancellationToken ct = default)
    {
        var result = await _supplierService.GetSuppliersAsync(query, ct);
        return SuccessResponse(SystemMessages.SuppliersRetrieved, result);
    }

    /// <summary>
    /// API-NCC-02: Xem thông tin chi tiết nhà cung cấp theo ID kèm lịch sử đơn mua PO.
    /// GET /api/v1/suppliers/{id}
    /// </summary>
    [HttpGet("{id:int}")]
    [Authorize]
    public async Task<ActionResult<ApiResponseModel<SupplierDetailDto>>> GetSupplierById(int id, CancellationToken ct = default)
    {
        var supplier = await _supplierService.GetSupplierByIdAsync(id, ct);
        if (supplier is null)
            throw new NotFoundException($"Supplier with id {id} was not found.");

        return SuccessResponse(SystemMessages.SupplierRetrieved, supplier);
    }

    /// <summary>
    /// API-NCC-03: Xem thông tin chi tiết nhà cung cấp theo mã NCC kèm lịch sử đơn mua PO.
    /// GET /api/v1/suppliers/by-code/{code}
    /// </summary>
    [HttpGet("by-code/{code}")]
    [Authorize]
    public async Task<ActionResult<ApiResponseModel<SupplierDetailDto>>> GetSupplierByCode(string code, CancellationToken ct = default)
    {
        var supplier = await _supplierService.GetSupplierByCodeAsync(code, ct);
        if (supplier is null)
            throw new NotFoundException($"Supplier with code '{code}' was not found.");

        return SuccessResponse(SystemMessages.SupplierRetrieved, supplier);
    }

    /// <summary>
    /// API-NCC-04: Thêm mới nhà cung cấp.
    /// POST /api/v1/suppliers
    /// </summary>
    [HttpPost]
    [Authorize(Roles = RoleConstants.Admin + "," + RoleConstants.Manager)]
    public async Task<ActionResult<ApiResponseModel<SupplierResponseDto>>> CreateSupplier(
        [FromBody] CreateSupplierRequest request, CancellationToken ct = default)
    {
        var (dto, error) = await _supplierService.CreateSupplierAsync(request, ct);

        if (error is not null)
            throw new BadRequestException(error);

        return CreatedResponse(
            nameof(GetSupplierById),
            new { id = dto!.SupplierId },
            SystemMessages.SupplierCreated,
            dto);
    }

    /// <summary>
    /// API-NCC-05: Cập nhật thông tin nhà cung cấp.
    /// PUT /api/v1/suppliers/{id}
    /// </summary>
    [HttpPut("{id:int}")]
    [Authorize(Roles = RoleConstants.Admin + "," + RoleConstants.Manager)]
    public async Task<ActionResult<ApiResponseModel<SupplierResponseDto>>> UpdateSupplier(
        int id,
        [FromBody] UpdateSupplierRequest request,
        CancellationToken ct = default)
    {
        var (dto, error) = await _supplierService.UpdateSupplierAsync(id, request, ct);

        if (dto is null && error is null)
            throw new NotFoundException($"Supplier with id {id} was not found.");

        if (error is not null)
            throw new BadRequestException(error);

        return SuccessResponse(SystemMessages.SupplierUpdated, dto!);
    }

    /// <summary>
    /// API-NCC-06: Xóa hoặc ngưng hoạt động nhà cung cấp.
    /// DELETE /api/v1/suppliers/{id}
    /// </summary>
    [HttpDelete("{id:int}")]
    [Authorize(Roles = RoleConstants.Admin + "," + RoleConstants.Manager)]
    public async Task<ActionResult<ApiResponseModel<object?>>> DeleteSupplier(int id, CancellationToken ct = default)
    {
        var (success, isDeactivated, error) = await _supplierService.DeleteSupplierAsync(id, ct);

        if (!success)
            throw new NotFoundException(error ?? $"Supplier with id {id} was not found.");

        var message = isDeactivated
            ? new SystemMessage(200, "Nhà cung cấp đã có lịch sử đơn mua hàng, đã chuyển sang trạng thái ngưng hoạt động.", true)
            : SystemMessages.SupplierDeleted;

        return SuccessResponse(message);
    }
}


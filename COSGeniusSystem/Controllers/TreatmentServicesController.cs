using COSGeniusSystem.Commons;
using COSGeniusSystem.DTOs.TreatmentServices;
using COSGeniusSystem.Exceptions;
using COSGeniusSystem.Services.TreatmentServices;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace COSGeniusSystem.Controllers;

[ApiController]
[Route("api/v1/services")]
public class TreatmentServicesController : BaseApiController
{
    private readonly ITreatmentService _treatmentService;

    public TreatmentServicesController(ITreatmentService treatmentService)
    {
        _treatmentService = treatmentService;
    }

    /// <summary>
    /// API-PHUC-03: Danh mục bảng giá dịch vụ trị liệu.
    /// GET /api/v1/services
    /// </summary>
    [HttpGet]
    [Authorize]
    public async Task<ActionResult<ApiResponseModel<List<TreatmentServiceResponseDto>>>> GetServices(
        [FromQuery] TreatmentServiceFilterQuery query, CancellationToken ct = default)
    {
        var result = await _treatmentService.GetServicesAsync(query, ct);
        return SuccessResponse(SystemMessages.ServicesRetrieved, result);
    }

    /// <summary>
    /// API-PHUC-03-DETAIL: Xem chi tiết dịch vụ theo ID.
    /// GET /api/v1/services/{id}
    /// </summary>
    [HttpGet("{id:int}")]
    [Authorize]
    public async Task<ActionResult<ApiResponseModel<TreatmentServiceResponseDto>>> GetServiceById(int id, CancellationToken ct = default)
    {
        var service = await _treatmentService.GetServiceByIdAsync(id, ct);
        if (service is null)
            throw new NotFoundException($"Treatment service with id {id} was not found.");

        return SuccessResponse(SystemMessages.ServiceRetrieved, service);
    }

    /// <summary>
    /// API-PHUC-03-CODE: Xem chi tiết dịch vụ theo mã dịch vụ.
    /// GET /api/v1/services/by-code/{code}
    /// </summary>
    [HttpGet("by-code/{code}")]
    [Authorize]
    public async Task<ActionResult<ApiResponseModel<TreatmentServiceResponseDto>>> GetServiceByCode(string code, CancellationToken ct = default)
    {
        var service = await _treatmentService.GetServiceByCodeAsync(code, ct);
        if (service is null)
            throw new NotFoundException($"Treatment service with code '{code}' was not found.");

        return SuccessResponse(SystemMessages.ServiceRetrieved, service);
    }

    /// <summary>
    /// API-PHUC-04: Thêm mới dịch vụ vào bảng giá.
    /// POST /api/v1/services
    /// </summary>
    [HttpPost]
    [Authorize(Roles = RoleConstants.Admin + "," + RoleConstants.Manager)]
    public async Task<ActionResult<ApiResponseModel<TreatmentServiceResponseDto>>> CreateService(
        [FromBody] CreateTreatmentServiceRequest request, CancellationToken ct = default)
    {
        var (dto, error) = await _treatmentService.CreateServiceAsync(request, ct);

        if (error is not null)
            throw new BadRequestException(error);

        return CreatedResponse(nameof(GetServiceById), new { id = dto!.ServiceId }, SystemMessages.ServiceCreated, dto);
    }

    /// <summary>
    /// API-PHUC-05: Cập nhật dịch vụ trị liệu.
    /// PUT /api/v1/services/{id}
    /// </summary>
    [HttpPut("{id:int}")]
    [Authorize(Roles = RoleConstants.Admin + "," + RoleConstants.Manager)]
    public async Task<ActionResult<ApiResponseModel<TreatmentServiceResponseDto>>> UpdateService(
        int id,
        [FromBody] UpdateTreatmentServiceRequest request,
        CancellationToken ct = default)
    {
        var (dto, error) = await _treatmentService.UpdateServiceAsync(id, request, ct);

        // dto == null && error == null → không tìm thấy → 404
        if (dto is null && error is null)
            throw new NotFoundException($"Service with id {id} was not found.");

        // dto == null && error != null → lỗi validation (price, trùng tên) → 400
        if (error is not null)
            throw new BadRequestException(error);

        return SuccessResponse(SystemMessages.ServiceUpdated, dto!);
    }

    /// <summary>
    /// API-PHUC-06: Xóa hoặc ngưng hoạt động dịch vụ trị liệu.
    /// DELETE /api/v1/services/{id}
    /// </summary>
    [HttpDelete("{id:int}")]
    [Authorize(Roles = RoleConstants.Admin + "," + RoleConstants.Manager)]
    public async Task<ActionResult<ApiResponseModel<object?>>> DeleteService(int id, CancellationToken ct = default)
    {
        var (success, isDeactivated, error) = await _treatmentService.DeleteServiceAsync(id, ct);

        if (!success)
            throw new NotFoundException(error ?? $"Service with id {id} was not found.");

        var message = isDeactivated
            ? new SystemMessage(200, "Dịch vụ đã có chỉ định trong liệu trình điều trị, đã chuyển sang trạng thái tạm ngưng.", true)
            : SystemMessages.ServiceDeleted;

        return SuccessResponse(message);
    }
}

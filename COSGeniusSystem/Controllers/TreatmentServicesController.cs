using COSGeniusSystem.Commons;
using COSGeniusSystem.Exceptions;
using COSGeniusSystem.DTOs.TreatmentServices;
using COSGeniusSystem.Services.TreatmentServices;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;

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
    /// API-PHUC-03: Danh mục bảng giá dịch vụ trị liệu (IsActive == true).
    /// GET /api/v1/services
    /// </summary>
    [HttpGet]
    [Authorize]
    public async Task<ActionResult<ApiResponseModel<List<TreatmentServiceResponseDto>>>> GetServices(CancellationToken ct = default)
    {
        var result = await _treatmentService.GetActiveServicesAsync(ct);
        return SuccessResponse(SystemMessages.ServicesRetrieved, result);
    }

    /// <summary>
    /// API-PHUC-04: Thêm mới dịch vụ vào bảng giá.
    /// POST /api/v1/services
    /// </summary>
    [HttpPost]
    public async Task<ActionResult<ApiResponseModel<TreatmentServiceResponseDto>>> CreateService(
        [FromBody] CreateTreatmentServiceRequest request)
    {
        var (dto, error) = await _treatmentService.CreateServiceAsync(request);

        if (error is not null)
            throw new BadRequestException(error);

        return CreatedResponse(nameof(GetServices), new { }, SystemMessages.ServiceCreated, dto ?? throw new InternalServerErrorException());
    }

    /// <summary>
    /// API-PHUC-05: Cập nhật dịch vụ trị liệu.
    /// PUT /api/v1/services/{id}
    /// </summary>
    [HttpPut("{id:int}")]
    public async Task<ActionResult<ApiResponseModel<TreatmentServiceResponseDto>>> UpdateService(
        int id,
        [FromBody] UpdateTreatmentServiceRequest request)
    {
        var (dto, error) = await _treatmentService.UpdateServiceAsync(id, request);

        // dto == null && error == null → không tìm thấy → 404
        if (dto is null && error is null)
            throw new NotFoundException($"Service with id {id} was not found.");

        // dto == null && error != null → lỗi validation (price, trùng tên) → 400
        if (error is not null)
            throw new BadRequestException(error);

        return SuccessResponse(SystemMessages.ServiceUpdated, dto!);
    }
}

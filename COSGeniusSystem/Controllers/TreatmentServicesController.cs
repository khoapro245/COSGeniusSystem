using COSGeniusSystem.DTOs.TreatmentServices;
using COSGeniusSystem.Services.TreatmentServices;
using Microsoft.AspNetCore.Mvc;

namespace COSGeniusSystem.Controllers;

[ApiController]
[Route("api/v1/services")]
public class TreatmentServicesController : ControllerBase
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
    public async Task<ActionResult<List<TreatmentServiceResponseDto>>> GetServices()
    {
        var result = await _treatmentService.GetActiveServicesAsync();
        return Ok(result);
    }

    /// <summary>
    /// API-PHUC-04: Thêm mới dịch vụ vào bảng giá.
    /// POST /api/v1/services
    /// </summary>
    [HttpPost]
    public async Task<ActionResult<TreatmentServiceResponseDto>> CreateService(
        [FromBody] CreateTreatmentServiceRequest request)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        var (dto, error) = await _treatmentService.CreateServiceAsync(request);

        if (error is not null)
            return BadRequest(new { message = error });

        return CreatedAtAction(
            nameof(GetServices),
            null,
            dto);
    }

    /// <summary>
    /// API-PHUC-05: Cập nhật dịch vụ trị liệu.
    /// PUT /api/v1/services/{id}
    /// </summary>
    [HttpPut("{id:int}")]
    public async Task<ActionResult<TreatmentServiceResponseDto>> UpdateService(
        int id,
        [FromBody] UpdateTreatmentServiceRequest request)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        var (dto, error) = await _treatmentService.UpdateServiceAsync(id, request);

        // dto == null && error == null → không tìm thấy → 404
        if (dto is null && error is null)
            return NotFound(new { message = $"Service with id {id} was not found." });

        // dto == null && error != null → lỗi validation (price, trùng tên) → 400
        if (error is not null)
            return BadRequest(new { message = error });

        return Ok(dto);
    }
}

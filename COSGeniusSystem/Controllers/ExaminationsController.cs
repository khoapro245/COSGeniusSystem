using COSGeniusSystem.Commons;
using COSGeniusSystem.DTOs.Examinations;
using COSGeniusSystem.Services.Examinations;
using Microsoft.AspNetCore.Mvc;

namespace COSGeniusSystem.Controllers;

[ApiController]
[Route("api/v1/examinations")]
public sealed class ExaminationsController : BaseApiController
{
    private readonly IExaminationService _examinationService;

    public ExaminationsController(IExaminationService examinationService)
    {
        _examinationService = examinationService;
    }

    /// <summary>
    /// Lấy danh sách đăng ký khám & hàng đợi khám bệnh.
    /// GET /api/v1/examinations/visits
    /// </summary>
    [HttpGet("visits")]
    public async Task<ActionResult<ApiResponseModel<List<ExaminationVisitDto>>>> GetVisits(
        [FromQuery] ExaminationFilterQuery query, CancellationToken ct = default)
    {
        var result = await _examinationService.GetVisitsAsync(query, ct);
        return SuccessResponse(SystemMessages.ExaminationVisitsRetrieved, result);
    }

    /// <summary>
    /// Đăng ký lượt khám bệnh mới vào hàng đợi.
    /// POST /api/v1/examinations/visits
    /// </summary>
    [HttpPost("visits")]
    public async Task<ActionResult<ApiResponseModel<ExaminationVisitDto>>> CreateVisit(
        [FromBody] CreateExaminationVisitRequest request, CancellationToken ct = default)
    {
        var result = await _examinationService.CreateVisitAsync(request, ct);
        return CreatedResponse(nameof(GetVisitById), new { examSheetId = result.ExamSheetId }, SystemMessages.ExaminationVisitCreated, result);
    }

    /// <summary>
    /// Xem chi tiết phiếu đăng ký khám theo ExamSheetID.
    /// GET /api/v1/examinations/visits/{examSheetId}
    /// </summary>
    [HttpGet("visits/{examSheetId:int}")]
    public async Task<ActionResult<ApiResponseModel<ExaminationVisitDto>>> GetVisitById(
        int examSheetId, CancellationToken ct = default)
    {
        var result = await _examinationService.GetVisitByIdAsync(examSheetId, ct);
        return SuccessResponse(SystemMessages.ExaminationVisitRetrieved, result);
    }

    /// <summary>
    /// Hủy lượt đăng ký khám nếu bệnh nhân bỏ về (chỉ áp dụng khi còn ở trạng thái 'Chờ khám').
    /// DELETE /api/v1/examinations/visits/{examSheetId}
    /// </summary>
    [HttpDelete("visits/{examSheetId:int}")]
    public async Task<ActionResult<ApiResponseModel<object?>>> CancelVisit(
        int examSheetId, CancellationToken ct = default)
    {
        await _examinationService.CancelVisitAsync(examSheetId, ct);
        return SuccessResponse(SystemMessages.ExaminationVisitCancelled);
    }
}


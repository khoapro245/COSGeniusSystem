using COSGeniusSystem.Commons;
using COSGeniusSystem.DTOs.Treatments;
using COSGeniusSystem.Services.Treatments;
using Microsoft.AspNetCore.Mvc;

namespace COSGeniusSystem.Controllers;

[ApiController]
[Route("api/v1/treatments")]
public sealed class TreatmentsController : BaseApiController
{
    private readonly ITreatmentProtocolService _treatmentProtocolService;

    public TreatmentsController(ITreatmentProtocolService treatmentProtocolService)
    {
        _treatmentProtocolService = treatmentProtocolService;
    }

    /// <summary>
    /// Lấy danh sách hàng đợi chờ điều trị/trị liệu theo liệu trình trong ngày.
    /// GET /api/v1/treatments/queue
    /// </summary>
    [HttpGet("queue")]
    public async Task<ActionResult<ApiResponseModel<List<TreatmentQueueDto>>>> GetQueue(
        [FromQuery] TreatmentQueueQuery query, CancellationToken ct = default)
    {
        var result = await _treatmentProtocolService.GetQueueAsync(query, ct);
        return SuccessResponse(SystemMessages.TreatmentQueueRetrieved, result);
    }

    /// <summary>
    /// Tiếp nhận bệnh nhân vào hàng đợi điều trị/trị liệu theo liệu trình.
    /// POST /api/v1/treatments/queue
    /// </summary>
    [HttpPost("queue")]
    public async Task<ActionResult<ApiResponseModel<TreatmentQueueDto>>> CreateTreatmentRequest(
        [FromBody] CreateTreatmentRequest request, CancellationToken ct = default)
    {
        var result = await _treatmentProtocolService.CreateTreatmentRequestAsync(request, ct);
        return CreatedResponse(nameof(GetQueue), new { }, SystemMessages.TreatmentRequestCreated, result);
    }
}


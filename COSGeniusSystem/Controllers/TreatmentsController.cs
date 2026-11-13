using Microsoft.AspNetCore.Authorization;

using COSGeniusSystem.Commons;

using COSGeniusSystem.DTOs;
using COSGeniusSystem.Services;
using Microsoft.AspNetCore.Mvc;

namespace COSGeniusSystem.Controllers;

[ApiController]
[Authorize(Roles = RoleConstants.ReceptionStaff + "," + RoleConstants.Doctor)]
[Route("api/v1/treatments")]
[Route("api/treatments")]
public sealed class TreatmentsController : BaseApiController
{
    private readonly ITreatmentProtocolService _treatmentProtocolService;

    public TreatmentsController(ITreatmentProtocolService treatmentProtocolService)
    {
        _treatmentProtocolService = treatmentProtocolService;
    }

    /// <summary>
    /// Lấy danh sách hàng đợi chờ điều trị/trị liệu theo liệu trình trong ngày.
    /// GET /api/v1/treatments/queue | GET /api/treatments/queue
    /// </summary>
    [HttpGet("queue")]
    public async Task<ActionResult<ApiResponseModel<List<TreatmentQueueDto>>>> GetQueue(
        [FromQuery] TreatmentQueueQuery query, CancellationToken ct = default)
    {
        var result = await _treatmentProtocolService.GetQueueAsync(User, query, ct);
        return SuccessResponse(SystemMessages.TreatmentQueueRetrieved, result);
    }

    /// <summary>
    /// Tiếp nhận bệnh nhân vào hàng đợi điều trị/trị liệu theo liệu trình.
    /// POST /api/v1/treatments/queue | POST /api/treatments/queue
    /// </summary>
    [HttpPost("queue")]
    [Authorize(Roles = RoleConstants.ReceptionStaff)]
    public async Task<ActionResult<ApiResponseModel<TreatmentQueueDto>>> CreateTreatmentRequest(
        [FromBody] CreateTreatmentRequest request, CancellationToken ct = default)
    {
        var result = await _treatmentProtocolService.CreateTreatmentRequestAsync(User, request, ct);
        return CreatedResponse(nameof(GetQueue), new { }, SystemMessages.TreatmentRequestCreated, result);
    }
}

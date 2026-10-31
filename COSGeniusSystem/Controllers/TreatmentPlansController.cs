using COSGeniusSystem.Common;
using COSGeniusSystem.Commons;
using COSGeniusSystem.DTOs.Treatments;
using COSGeniusSystem.Services.Treatments;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace COSGeniusSystem.Controllers;

[Route("api/v1/treatment-plans")]
[Authorize(Roles = RoleConstants.ReceptionStaff + "," + RoleConstants.Doctor)]
public sealed class TreatmentPlansController(ITreatmentWorkflowService service) : BaseApiController
{
    [HttpGet]
    public async Task<ActionResult<ApiResponseModel<PagedResult<PlanSummary>>>> List([FromQuery] PlanQuery query, CancellationToken ct) =>
        SuccessResponse(SystemMessages.TreatmentsRetrieved, await service.PlansAsync(User, query, ct));
    [HttpGet("{id:int:min(1)}")]
    public async Task<ActionResult<ApiResponseModel<PlanOverview>>> Get(int id, CancellationToken ct) =>
        SuccessResponse(SystemMessages.TreatmentsRetrieved, await service.PlanAsync(User, id, ct));
    [HttpPost("{id:int:min(1)}/cancel")]
    [Authorize(Roles = RoleConstants.Doctor)]
    public async Task<ActionResult<ApiResponseModel<PlanOverview>>> Cancel(int id, CancelTreatment request, CancellationToken ct) =>
        SuccessResponse(SystemMessages.TreatmentSaved, await service.CancelPlanAsync(User, id, request, ct));
    [HttpPost("{id:int:min(1)}/sessions")]
    [Authorize(Roles = RoleConstants.ReceptionStaff)]
    public async Task<ActionResult<ApiResponseModel<SessionOverview>>> CreateRequest(int id, RequestTreatment request, CancellationToken ct) =>
        SuccessResponse(SystemMessages.TreatmentSaved, await service.RequestAsync(User, id, request, ct));
}

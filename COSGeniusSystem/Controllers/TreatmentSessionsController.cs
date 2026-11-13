using COSGeniusSystem.Commons;
using COSGeniusSystem.DTOs;
using COSGeniusSystem.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace COSGeniusSystem.Controllers;

[Route("api/v1/treatment-sessions")]
[Route("api/treatment-sessions")]
[Authorize(Roles = RoleConstants.ReceptionStaff + "," + RoleConstants.Doctor)]
public sealed class TreatmentSessionsController(ITreatmentWorkflowService service) : BaseApiController
{
    [HttpGet]
    public async Task<ActionResult<ApiResponseModel<PagedResult<SessionSummary>>>> List([FromQuery] SessionQuery query, CancellationToken ct) =>
        SuccessResponse(SystemMessages.TreatmentsRetrieved, await service.SessionsAsync(User, query, false, ct));

    [HttpGet("doctor-workload")]
    [Authorize(Roles = RoleConstants.ReceptionStaff)]
    public async Task<ActionResult<ApiResponseModel<IReadOnlyList<TreatmentWorkload>>>> Workload([FromQuery] TreatmentWorkloadQuery query, CancellationToken ct) =>
        SuccessResponse(SystemMessages.TreatmentsRetrieved, await service.WorkloadAsync(User, query, ct));

    [HttpGet("mine")]
    [Authorize(Roles = RoleConstants.Doctor)]
    public async Task<ActionResult<ApiResponseModel<PagedResult<SessionSummary>>>> Mine([FromQuery] SessionQuery query, CancellationToken ct) =>
        SuccessResponse(SystemMessages.TreatmentsRetrieved, await service.SessionsAsync(User, query, true, ct));

    [HttpGet("{id:int:min(1)}")]
    public async Task<ActionResult<ApiResponseModel<SessionOverview>>> Get(int id, CancellationToken ct) =>
        SuccessResponse(SystemMessages.TreatmentsRetrieved, await service.SessionAsync(User, id, ct));

    [HttpPost("{id:int:min(1)}/start")]
    [Authorize(Roles = RoleConstants.Doctor)]
    public async Task<ActionResult<ApiResponseModel<SessionOverview>>> Start(int id, VersionRequest request, CancellationToken ct) =>
        SuccessResponse(SystemMessages.TreatmentSaved, await service.StartAsync(User, id, request, ct));

    [HttpPost("{id:int:min(1)}/complete")]
    [Authorize(Roles = RoleConstants.Doctor)]
    public async Task<ActionResult<ApiResponseModel<SessionOverview>>> Complete(int id, CompleteTreatment request, CancellationToken ct) =>
        SuccessResponse(SystemMessages.TreatmentSaved, await service.CompleteAsync(User, id, request, ct));

    [HttpPost("{id:int:min(1)}/cancel")]
    public async Task<ActionResult<ApiResponseModel<SessionOverview>>> Cancel(int id, CancelTreatment request, CancellationToken ct) =>
        SuccessResponse(SystemMessages.TreatmentSaved, await service.CancelAsync(User, id, request, ct));

    [HttpPatch("{id:int:min(1)}/cancel")]
    [HttpPatch("usage/{id:int:min(1)}/cancel")]
    [HttpPost("usage/{id:int:min(1)}/cancel")]
    [Authorize(Roles = RoleConstants.ReceptionStaff + "," + RoleConstants.Doctor)]
    public async Task<ActionResult<ApiResponseModel<SessionOverview>>> CancelTicket(int id,
        [FromBody] CancelTicketRequest request, CancellationToken ct) =>
        SuccessResponse(SystemMessages.TreatmentCancelled, await service.CancelSessionTicketAsync(User, id, request, ct));

    [HttpDelete("{id:int:min(1)}")]
    [HttpDelete("usage/{id:int:min(1)}")]
    [Authorize(Roles = RoleConstants.ReceptionStaff + "," + RoleConstants.Doctor)]
    public async Task<ActionResult<ApiResponseModel<SessionOverview>>> DeleteOrCancel(int id, CancellationToken ct) =>
        SuccessResponse(SystemMessages.TreatmentCancelled, await service.CancelSessionTicketAsync(User, id,
            new CancelTicketRequest { CancelReason = "Bệnh nhân hủy buổi điều trị." }, ct));
}

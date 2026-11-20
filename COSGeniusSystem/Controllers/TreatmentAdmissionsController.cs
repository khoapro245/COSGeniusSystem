using COSGeniusSystem.Commons;
using COSGeniusSystem.DTOs;
using COSGeniusSystem.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace COSGeniusSystem.Controllers;

[Route("api/v1/treatment-admissions")]
[Authorize(Roles = RoleConstants.ReceptionStaff + "," + RoleConstants.Doctor)]
public sealed class TreatmentAdmissionsController(ITreatmentWorkflowService service) : BaseApiController
{
    [HttpPost, Authorize(Roles = RoleConstants.ReceptionStaff)]
    public async Task<ActionResult<ApiResponseModel<IReadOnlyList<AdmissionOverview>>>> Create(CreateTreatmentAdmission request, CancellationToken ct) =>
        SuccessResponse(SystemMessages.TreatmentSaved, await service.CreateAdmissionsAsync(User, request, ct));

    [HttpGet]
    public async Task<ActionResult<ApiResponseModel<PagedResult<AdmissionSummary>>>> List([FromQuery] SessionQuery query, CancellationToken ct) =>
        SuccessResponse(SystemMessages.TreatmentsRetrieved, await service.AdmissionsAsync(User, query, ct));

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<ApiResponseModel<AdmissionOverview>>> Get(Guid id, CancellationToken ct) =>
        SuccessResponse(SystemMessages.TreatmentsRetrieved, await service.AdmissionAsync(User, id, ct));

    [HttpPost("{id:guid}/accept"), Authorize(Roles = RoleConstants.Doctor)]
    public async Task<ActionResult<ApiResponseModel<AdmissionOverview>>> Accept(Guid id, VersionRequest request, CancellationToken ct) =>
        SuccessResponse(SystemMessages.TreatmentSaved, await service.UpdateAdmissionAsync(User, id, request, false, ct));

    [HttpPost("{id:guid}/close"), Authorize(Roles = RoleConstants.Doctor)]
    public async Task<ActionResult<ApiResponseModel<AdmissionOverview>>> Close(Guid id, VersionRequest request, CancellationToken ct) =>
        SuccessResponse(SystemMessages.TreatmentSaved, await service.UpdateAdmissionAsync(User, id, request, true, ct));
}

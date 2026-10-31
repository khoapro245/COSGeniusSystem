using COSGeniusSystem.Commons;
using COSGeniusSystem.DTOs.Examinations;
using COSGeniusSystem.Services.Examinations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace COSGeniusSystem.Controllers;

[ApiController]
[Route("api/v1/examinations")]
[Authorize(Roles = RoleConstants.ReceptionStaff + "," + RoleConstants.Doctor)]
public sealed class ExaminationsController(IExaminationService service) : BaseApiController
{
    [HttpGet]
    public async Task<ActionResult<ApiResponseModel<PagedResult<RegistrationResponse>>>> List(
        [FromQuery] RegistrationQuery query, CancellationToken ct) =>
        SuccessResponse(ExaminationMessages.Retrieved, await service.ListAsync(User, query, false, ct));

    [HttpGet("mine")]
    [Authorize(Roles = RoleConstants.Doctor)]
    public async Task<ActionResult<ApiResponseModel<PagedResult<RegistrationResponse>>>> Mine(
        [FromQuery] RegistrationQuery query, CancellationToken ct) =>
        SuccessResponse(ExaminationMessages.Retrieved, await service.ListAsync(User, query, true, ct));

    [HttpPost]
    [Authorize(Roles = RoleConstants.ReceptionStaff)]
    public async Task<ActionResult<ApiResponseModel<RegistrationResponse>>> Create(CreateRegistrationRequest request, CancellationToken ct)
    {
        var result = await service.CreateAsync(User, request, ct);
        return CreatedResponse(nameof(Get), new { registrationId = result.RegistrationId }, ExaminationMessages.Created, result);
    }

    [HttpGet("{registrationId:int:min(1)}")]
    public async Task<ActionResult<ApiResponseModel<ExaminationResponse>>> Get(int registrationId, CancellationToken ct) =>
        SuccessResponse(ExaminationMessages.Retrieved, await service.GetAsync(User, registrationId, ct));

    [HttpPut("{registrationId:int:min(1)}")]
    [Authorize(Roles = RoleConstants.ReceptionStaff)]
    public async Task<ActionResult<ApiResponseModel<RegistrationResponse>>> Update(int registrationId,
        UpdateRegistrationRequest request, CancellationToken ct) =>
        SuccessResponse(ExaminationMessages.Saved, await service.UpdateAsync(User, registrationId, request, ct));

    [HttpGet("{registrationId:int:min(1)}/history")]
    [Authorize(Roles = RoleConstants.Doctor)]
    public async Task<ActionResult<ApiResponseModel<PagedResult<MedicalRecordResponse>>>> History(int registrationId,
        [FromQuery] PageQuery query, CancellationToken ct) =>
        SuccessResponse(ExaminationMessages.Retrieved, await service.HistoryAsync(User, registrationId, query, ct));

    [HttpPost("{registrationId:int:min(1)}/start")]
    [Authorize(Roles = RoleConstants.Doctor)]
    public async Task<ActionResult<ApiResponseModel<MedicalRecordResponse>>> Start(int registrationId, CancellationToken ct) =>
        SuccessResponse(ExaminationMessages.Started, await service.StartAsync(User, registrationId, ct));

    [HttpGet("{registrationId:int:min(1)}/history/{recordId:int:min(1)}")]
    [Authorize(Roles = RoleConstants.Doctor)]
    public async Task<ActionResult<ApiResponseModel<ExaminationResponse>>> HistoryDetail(int registrationId, int recordId, CancellationToken ct) =>
        SuccessResponse(ExaminationMessages.Retrieved, await service.HistoryDetailAsync(User, registrationId, recordId, ct));

    [HttpPut("{registrationId:int:min(1)}/medical-record")]
    [Authorize(Roles = RoleConstants.Doctor)]
    public async Task<ActionResult<ApiResponseModel<MedicalRecordResponse>>> Save(int registrationId,
        SaveMedicalRecordRequest request, CancellationToken ct) =>
        SuccessResponse(ExaminationMessages.Saved, await service.SaveAsync(User, registrationId, request, ct));

    [HttpPost("{registrationId:int:min(1)}/complete")]
    [Authorize(Roles = RoleConstants.Doctor)]
    public async Task<ActionResult<ApiResponseModel<MedicalRecordResponse>>> Complete(int registrationId,
        VersionRequest request, CancellationToken ct) =>
        SuccessResponse(ExaminationMessages.Completed, await service.CompleteAsync(User, registrationId, request, ct));

    [HttpGet("herbal-formulas")]
    [Authorize(Roles = RoleConstants.Doctor)]
    public async Task<ActionResult<ApiResponseModel<PagedResult<FormulaResponse>>>> Formulas([FromQuery] PageQuery query, CancellationToken ct) =>
        SuccessResponse(ExaminationMessages.Retrieved, await service.FormulasAsync(query, ct));

    [HttpPost("{registrationId:int:min(1)}/prescriptions")]
    [Authorize(Roles = RoleConstants.Doctor)]
    public async Task<ActionResult<ApiResponseModel<PrescriptionResponse>>> CreatePrescription(int registrationId, CancellationToken ct) =>
        SuccessResponse(ExaminationMessages.DraftReady, await service.CreatePrescriptionAsync(User, registrationId, ct));

    [HttpPut("{registrationId:int:min(1)}/prescriptions/{prescriptionId:int:min(1)}")]
    [Authorize(Roles = RoleConstants.Doctor)]
    public async Task<ActionResult<ApiResponseModel<PrescriptionResponse>>> SavePrescription(int registrationId, int prescriptionId,
        SavePrescriptionRequest request, CancellationToken ct) =>
        SuccessResponse(ExaminationMessages.Saved, await service.SavePrescriptionAsync(User, registrationId, prescriptionId, request, ct));

    [HttpPost("{registrationId:int:min(1)}/prescriptions/{prescriptionId:int:min(1)}/load-formula")]
    [Authorize(Roles = RoleConstants.Doctor)]
    public async Task<ActionResult<ApiResponseModel<PrescriptionResponse>>> LoadFormula(int registrationId, int prescriptionId,
        LoadFormulaRequest request, CancellationToken ct) =>
        SuccessResponse(ExaminationMessages.Saved, await service.LoadFormulaAsync(User, registrationId, prescriptionId, request, ct));

    [HttpPost("{registrationId:int:min(1)}/prescriptions/{prescriptionId:int:min(1)}/send")]
    [Authorize(Roles = RoleConstants.Doctor)]
    public async Task<ActionResult<ApiResponseModel<PrescriptionResponse>>> Send(int registrationId, int prescriptionId,
        VersionRequest request, CancellationToken ct) =>
        SuccessResponse(ExaminationMessages.Sent, await service.SendPrescriptionAsync(User, registrationId, prescriptionId, request, ct));

    [HttpDelete("{registrationId:int:min(1)}/prescriptions/{prescriptionId:int:min(1)}")]
    [Authorize(Roles = RoleConstants.Doctor)]
    public async Task<ActionResult<ApiResponseModel<object?>>> DeletePrescription(int registrationId, int prescriptionId,
        [FromBody] VersionRequest request, CancellationToken ct)
    {
        await service.DeletePrescriptionAsync(User, registrationId, prescriptionId, request, ct);
        return SuccessResponse(ExaminationMessages.Deleted);
    }

    [HttpPost("{registrationId:int:min(1)}/treatment-plans")]
    [Authorize(Roles = RoleConstants.Doctor)]
    public async Task<ActionResult<ApiResponseModel<TreatmentPlanResponse>>> CreatePlan(int registrationId, CancellationToken ct) =>
        SuccessResponse(ExaminationMessages.DraftReady, await service.CreatePlanAsync(User, registrationId, ct));

    [HttpPut("{registrationId:int:min(1)}/treatment-plans/{planId:int:min(1)}")]
    [Authorize(Roles = RoleConstants.Doctor)]
    public async Task<ActionResult<ApiResponseModel<TreatmentPlanResponse>>> SavePlan(int registrationId, int planId,
        SaveTreatmentPlanRequest request, CancellationToken ct) =>
        SuccessResponse(ExaminationMessages.Saved, await service.SavePlanAsync(User, registrationId, planId, request, ct));

    [HttpPost("{registrationId:int:min(1)}/treatment-plans/{planId:int:min(1)}/confirm")]
    [Authorize(Roles = RoleConstants.Doctor)]
    public async Task<ActionResult<ApiResponseModel<TreatmentPlanResponse>>> Confirm(int registrationId, int planId,
        VersionRequest request, CancellationToken ct) =>
        SuccessResponse(ExaminationMessages.Confirmed, await service.ConfirmPlanAsync(User, registrationId, planId, request, ct));

    [HttpDelete("{registrationId:int:min(1)}/treatment-plans/{planId:int:min(1)}")]
    [Authorize(Roles = RoleConstants.Doctor)]
    public async Task<ActionResult<ApiResponseModel<object?>>> DeletePlan(int registrationId, int planId,
        [FromBody] VersionRequest request, CancellationToken ct)
    {
        await service.DeletePlanAsync(User, registrationId, planId, request, ct);
        return SuccessResponse(ExaminationMessages.Deleted);
    }
}


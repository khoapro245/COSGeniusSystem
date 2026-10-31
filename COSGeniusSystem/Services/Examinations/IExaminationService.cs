using System.Security.Claims;
using COSGeniusSystem.Commons;
using COSGeniusSystem.DTOs.Examinations;

namespace COSGeniusSystem.Services.Examinations;

public interface IExaminationService
{
    Task<PagedResult<RegistrationResponse>> ListAsync(ClaimsPrincipal actor, RegistrationQuery query, bool mine, CancellationToken ct);
    Task<RegistrationResponse> CreateAsync(ClaimsPrincipal actor, CreateRegistrationRequest request, CancellationToken ct);
    Task<RegistrationResponse> UpdateAsync(ClaimsPrincipal actor, int registrationId, UpdateRegistrationRequest request, CancellationToken ct);
    Task<ExaminationResponse> GetAsync(ClaimsPrincipal actor, int registrationId, CancellationToken ct);
    Task<PagedResult<MedicalRecordResponse>> HistoryAsync(ClaimsPrincipal actor, int registrationId, PageQuery query, CancellationToken ct);
    Task<ExaminationResponse> HistoryDetailAsync(ClaimsPrincipal actor, int registrationId, int recordId, CancellationToken ct);
    Task<MedicalRecordResponse> StartAsync(ClaimsPrincipal actor, int registrationId, CancellationToken ct);
    Task<MedicalRecordResponse> SaveAsync(ClaimsPrincipal actor, int registrationId, SaveMedicalRecordRequest request, CancellationToken ct);
    Task<MedicalRecordResponse> CompleteAsync(ClaimsPrincipal actor, int registrationId, VersionRequest request, CancellationToken ct);
    Task<PagedResult<FormulaResponse>> FormulasAsync(PageQuery query, CancellationToken ct);
    Task<PrescriptionResponse> CreatePrescriptionAsync(ClaimsPrincipal actor, int registrationId, CancellationToken ct);
    Task<PrescriptionResponse> SavePrescriptionAsync(ClaimsPrincipal actor, int registrationId, int prescriptionId, SavePrescriptionRequest request, CancellationToken ct);
    Task<PrescriptionResponse> LoadFormulaAsync(ClaimsPrincipal actor, int registrationId, int prescriptionId, LoadFormulaRequest request, CancellationToken ct);
    Task<PrescriptionResponse> SendPrescriptionAsync(ClaimsPrincipal actor, int registrationId, int prescriptionId, VersionRequest request, CancellationToken ct);
    Task DeletePrescriptionAsync(ClaimsPrincipal actor, int registrationId, int prescriptionId, VersionRequest request, CancellationToken ct);
    Task<TreatmentPlanResponse> CreatePlanAsync(ClaimsPrincipal actor, int registrationId, CancellationToken ct);
    Task<TreatmentPlanResponse> SavePlanAsync(ClaimsPrincipal actor, int registrationId, int planId, SaveTreatmentPlanRequest request, CancellationToken ct);
    Task<TreatmentPlanResponse> ConfirmPlanAsync(ClaimsPrincipal actor, int registrationId, int planId, VersionRequest request, CancellationToken ct);
    Task DeletePlanAsync(ClaimsPrincipal actor, int registrationId, int planId, VersionRequest request, CancellationToken ct);
}


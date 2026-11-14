using COSGeniusSystem.Commons;
using COSGeniusSystem.DTOs.DoctorClinical;
using COSGeniusSystem.Services.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace COSGeniusSystem.Controllers;

[ApiController]
[Route("api/v1/doctor")]
public sealed class DoctorClinicalController(IDoctorClinicalService service) : BaseApiController
{
    /// <summary>
    /// 1. Tra cứu danh sách đăng ký khám và hàng đợi khám bệnh của bác sĩ (DOC-01 & Cột trái DOC-06).
    /// GET /api/v1/doctor/examinations
    /// </summary>
    [HttpGet("examinations")]
    public async Task<ActionResult<ApiResponseModel<DoctorExamQueueResponseDto>>> GetExamQueue(
        [FromQuery] DoctorExamFilterDto query, CancellationToken ct = default)
    {
        var result = await service.GetExamQueueAsync(query, ct);
        return SuccessResponse(SystemMessages.DoctorQueueRetrieved, result);
    }

    /// <summary>
    /// 2. Nghiệp vụ nút "Gọi vào khám": Chuyển trạng thái Chờ khám -> Đang khám và mở hồ sơ.
    /// PATCH /api/v1/doctor/examinations/{examSheetId}/call
    /// </summary>
    [HttpPatch("examinations/{examSheetId:int}/call")]
    public async Task<ActionResult<ApiResponseModel<MedicalRecordWorkspaceDto>>> CallPatient(
        int examSheetId, CancellationToken ct = default)
    {
        var result = await service.CallPatientAsync(examSheetId, ct);
        return SuccessResponse(SystemMessages.PatientCalledForExam, result);
    }

    /// <summary>
    /// 3. Lấy toàn bộ hồ sơ khám bệnh workspace của một lượt khám (DOC-02, DOC-05, DOC-06).
    /// GET /api/v1/doctor/examinations/{examSheetId}
    /// </summary>
    [HttpGet("examinations/{examSheetId:int}")]
    public async Task<ActionResult<ApiResponseModel<MedicalRecordWorkspaceDto>>> GetMedicalRecordWorkspace(
        int examSheetId, CancellationToken ct = default)
    {
        var result = await service.GetMedicalRecordWorkspaceAsync(examSheetId, ct);
        return SuccessResponse(SystemMessages.MedicalRecordWorkspaceRetrieved, result);
    }

    /// <summary>
    /// 4. Lưu thông tin khám lâm sàng, sinh hiệu, tứ chẩn YHCT và chẩn đoán.
    /// PUT /api/v1/doctor/examinations/{examSheetId}/clinical
    /// </summary>
    [HttpPut("examinations/{examSheetId:int}/clinical")]
    public async Task<ActionResult<ApiResponseModel<MedicalRecordWorkspaceDto>>> SaveClinicalExam(
        int examSheetId, [FromBody] SaveClinicalExamRequestDto request, CancellationToken ct = default)
    {
        var result = await service.SaveClinicalExamAsync(examSheetId, request, ct);
        return SuccessResponse(SystemMessages.ClinicalExamSaved, result);
    }

    /// <summary>
    /// 5. Hoàn tất lượt khám bệnh và khóa hồ sơ (IsReadOnly = true).
    /// POST /api/v1/doctor/examinations/{examSheetId}/complete
    /// </summary>
    [HttpPost("examinations/{examSheetId:int}/complete")]
    public async Task<ActionResult<ApiResponseModel<MedicalRecordWorkspaceDto>>> CompleteExamination(
        int examSheetId, [FromBody] SaveClinicalExamRequestDto? request = null, CancellationToken ct = default)
    {
        var result = await service.CompleteExaminationAsync(examSheetId, request, ct);
        return SuccessResponse(SystemMessages.ExaminationCompleted, result);
    }

    /// <summary>
    /// 6. Kê dịch vụ điều trị (đơn lẻ hoặc theo liệu trình) tại Tab Kê dịch vụ (DOC-03).
    /// PUT /api/v1/doctor/examinations/{examSheetId}/services
    /// </summary>
    [HttpPut("examinations/{examSheetId:int}/services")]
    public async Task<ActionResult<ApiResponseModel<ServiceIndicationDto>>> SaveServiceIndication(
        int examSheetId, [FromBody] SaveServiceIndicationRequestDto request, CancellationToken ct = default)
    {
        var result = await service.SaveServiceIndicationAsync(examSheetId, request, ct);
        return SuccessResponse(SystemMessages.ServiceIndicationSaved, result);
    }

    /// <summary>
    /// 7. Lấy danh mục bài thuốc mẫu và danh sách vị dược liệu phục vụ Tab Kê đơn thuốc (DOC-04).
    /// GET /api/v1/doctor/prescriptions/catalog
    /// </summary>
    [HttpGet("prescriptions/catalog")]
    public async Task<ActionResult<ApiResponseModel<PrescriptionCatalogDto>>> GetPrescriptionCatalog(
        CancellationToken ct = default)
    {
        var result = await service.GetPrescriptionCatalogAsync(ct);
        return SuccessResponse(SystemMessages.PrescriptionCatalogRetrieved, result);
    }

    /// <summary>
    /// 8. Kê đơn thuốc (Lưu nháp hoặc Gửi Herbalist).
    /// PUT /api/v1/doctor/examinations/{examSheetId}/prescription
    /// </summary>
    [HttpPut("examinations/{examSheetId:int}/prescription")]
    public async Task<ActionResult<ApiResponseModel<PrescriptionDetailDto>>> SavePrescription(
        int examSheetId, [FromBody] SavePrescriptionRequestDto request, CancellationToken ct = default)
    {
        var result = await service.SavePrescriptionAsync(examSheetId, request, ct);
        return SuccessResponse(SystemMessages.PrescriptionSaved, result);
    }
}


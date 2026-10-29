using COSGeniusSystem.Commons;
using COSGeniusSystem.DTOs.Patients;
using COSGeniusSystem.Services.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace COSGeniusSystem.Controllers;

[ApiController]
[Route("api/v1/patients")]
[Route("api/patients")]
public sealed class PatientsController : BaseApiController
{
    private readonly IPatientService _patientService;

    public PatientsController(IPatientService patientService)
    {
        _patientService = patientService;
    }

    /// <summary>
    /// Lấy danh sách hồ sơ bệnh nhân (hỗ trợ tìm kiếm theo Mã BN, Tên, Số điện thoại).
    /// GET /api/v1/patients
    /// </summary>
    [HttpGet]
    public async Task<ActionResult<ApiResponseModel<List<PatientResponseDto>>>> GetPatients(
        [FromQuery] PatientSearchQuery query, CancellationToken ct = default)
    {
        var result = await _patientService.SearchPatientsAsync(query, ct);
        return SuccessResponse(SystemMessages.PatientsRetrieved, result);
    }

    /// <summary>
    /// Xem chi tiết thông tin bệnh nhân kèm danh sách người liên hệ.
    /// GET /api/v1/patients/{id}
    /// </summary>
    [HttpGet("{id:int}")]
    public async Task<ActionResult<ApiResponseModel<PatientDetailDto>>> GetPatientById(
        int id, CancellationToken ct = default)
    {
        var result = await _patientService.GetPatientDetailAsync(id, ct);
        return SuccessResponse(SystemMessages.PatientRetrieved, result);
    }

    /// <summary>
    /// Tạo mới hồ sơ bệnh nhân, tự động tạo MedicalRecord và thông tin người liên hệ.
    /// POST /api/v1/patients
    /// </summary>
    [HttpPost]
    public async Task<ActionResult<ApiResponseModel<PatientDetailDto>>> CreatePatient(
        [FromBody] CreatePatientRequest request, CancellationToken ct = default)
    {
        var result = await _patientService.CreatePatientV1Async(request, ct);
        return CreatedResponse(nameof(GetPatientById), new { id = result.PatientId }, SystemMessages.PatientCreated, result);
    }

    /// <summary>
    /// Cập nhật thông tin bệnh nhân và danh sách người liên hệ.
    /// PUT /api/v1/patients/{id}
    /// </summary>
    [HttpPut("{id:int}")]
    public async Task<ActionResult<ApiResponseModel<PatientDetailDto>>> UpdatePatient(
        int id, [FromBody] UpdatePatientRequest request, CancellationToken ct = default)
    {
        var result = await _patientService.UpdatePatientV1Async(id, request, ct);
        return SuccessResponse(SystemMessages.PatientUpdated, result);
    }

    /// <summary>
    /// Lịch sử các lần khám bệnh của bệnh nhân.
    /// GET /api/v1/patients/{id}/visit-history
    /// </summary>
    [HttpGet("{id:int}/visit-history")]
    public async Task<ActionResult<ApiResponseModel<List<PatientVisitHistoryDto>>>> GetVisitHistory(
        int id, CancellationToken ct = default)
    {
        var result = await _patientService.GetVisitHistoryAsync(id, ct);
        return SuccessResponse(SystemMessages.VisitHistoryRetrieved, result);
    }

    /// <summary>
    /// Xem chi tiết một lần khám bệnh (thông tin khám, dịch vụ chỉ định, đơn thuốc).
    /// GET /api/v1/patients/exam-sheets/{examSheetId}
    /// </summary>
    [HttpGet("exam-sheets/{examSheetId:int}")]
    public async Task<ActionResult<ApiResponseModel<MedicalRecordDetailDto>>> GetExamSheetDetail(
        int examSheetId, CancellationToken ct = default)
    {
        var result = await _patientService.GetExamSheetDetailAsync(examSheetId, ct);
        return SuccessResponse(SystemMessages.ExamSheetDetailRetrieved, result);
    }

    /// <summary>
    /// Danh sách phác đồ / liệu trình điều trị của bệnh nhân.
    /// GET /api/v1/patients/{id}/treatment-protocols
    /// </summary>
    [HttpGet("{id:int}/treatment-protocols")]
    public async Task<ActionResult<ApiResponseModel<List<TreatmentProtocolDto>>>> GetTreatmentProtocols(
        int id, CancellationToken ct = default)
    {
        var result = await _patientService.GetTreatmentProtocolsAsync(id, ct);
        return SuccessResponse(SystemMessages.TreatmentProtocolsRetrieved, result);
    }

    /// <summary>
    /// Chi tiết các buổi trị liệu đã thực hiện của một phác đồ.
    /// GET /api/v1/patients/treatment-protocols/{protocolId}/sessions
    /// </summary>
    [HttpGet("treatment-protocols/{protocolId:int}/sessions")]
    public async Task<ActionResult<ApiResponseModel<List<TreatmentSessionDto>>>> GetTreatmentSessions(
        int protocolId, CancellationToken ct = default)
    {
        var result = await _patientService.GetTreatmentSessionsAsync(protocolId, ct);
        return SuccessResponse(SystemMessages.TreatmentSessionsRetrieved, result);
    }

    /// <summary>
    /// Xóa hồ sơ bệnh nhân (chỉ xóa được khi không có lịch sử khám/liên hệ).
    /// DELETE /api/v1/patients/{patientId}
    /// </summary>
    [HttpDelete("{patientId:int:min(1)}")]
    public async Task<ActionResult<ApiResponseModel<object?>>> Delete(
        int patientId, CancellationToken ct = default)
    {
        await _patientService.DeleteAsync(patientId, ct);
        return SuccessResponse(SystemMessages.PatientDeleted);
    }
}

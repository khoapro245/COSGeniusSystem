using COSGeniusSystem.Commons;
using COSGeniusSystem.DTOs.Patients;
using COSGeniusSystem.DTOs.Requests;
using COSGeniusSystem.DTOs.Responses;

namespace COSGeniusSystem.Services.Interfaces;

public interface IPatientService
{
    // Legacy methods
    Task<PagedResult<PatientResponse>> GetAllAsync(int pageNumber, int pageSize, CancellationToken ct);
    Task<PatientResponse> GetByIdAsync(int patientId, CancellationToken ct);
    Task<PatientResponse> CreateAsync(COSGeniusSystem.DTOs.Requests.CreatePatientRequest request, CancellationToken ct);
    Task UpdateAsync(int patientId, COSGeniusSystem.DTOs.Requests.UpdatePatientRequest request, CancellationToken ct);
    Task DeleteAsync(int patientId, CancellationToken ct);

    // Module 1: Hồ sơ & Thông tin bệnh nhân (Phân hệ Tiếp tân)
    Task<List<PatientResponseDto>> SearchPatientsAsync(PatientSearchQuery query, CancellationToken ct = default);
    Task<PatientDetailDto> GetPatientDetailAsync(int id, CancellationToken ct = default);
    Task<PatientDetailDto> CreatePatientV1Async(COSGeniusSystem.DTOs.Patients.CreatePatientRequest request, CancellationToken ct = default);
    Task<PatientDetailDto> UpdatePatientV1Async(int id, COSGeniusSystem.DTOs.Patients.UpdatePatientRequest request, CancellationToken ct = default);
    Task<List<PatientVisitHistoryDto>> GetVisitHistoryAsync(int patientId, CancellationToken ct = default);
    Task<MedicalRecordDetailDto> GetExamSheetDetailAsync(int examSheetId, CancellationToken ct = default);
    Task<List<TreatmentProtocolDto>> GetTreatmentProtocolsAsync(int patientId, CancellationToken ct = default);
    Task<List<TreatmentSessionDto>> GetTreatmentSessionsAsync(int protocolId, CancellationToken ct = default);
}

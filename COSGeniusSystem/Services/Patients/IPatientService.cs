using COSGeniusSystem.Commons;
using COSGeniusSystem.DTOs;

namespace COSGeniusSystem.Services;

public interface IPatientService
{
    // Legacy methods
    Task<PagedResult<PatientResponse>> GetAllAsync(int pageNumber, int pageSize, CancellationToken ct);
    Task<PagedResult<PatientResponse>> SearchAsync(string? search, int pageNumber, int pageSize, CancellationToken ct, string? code = null, string? phone = null, string? fullName = null);
    Task<PatientResponse> GetByIdAsync(int patientId, CancellationToken ct);
    Task<PatientResponse> CreateAsync(COSGeniusSystem.DTOs.CreatePatientRequest request, CancellationToken ct);
    Task UpdateAsync(int patientId, COSGeniusSystem.DTOs.UpdatePatientRequest request, CancellationToken ct);
    Task DeleteAsync(int patientId, CancellationToken ct);

    // Module 1: Hồ sơ & Thông tin bệnh nhân (Phân hệ Tiếp tân)
    Task<List<PatientResponseDto>> SearchPatientsAsync(PatientSearchQuery query, CancellationToken ct = default);
    Task<PatientDetailDto> GetPatientDetailAsync(int id, CancellationToken ct = default);
    Task<PatientDetailDto> CreatePatientV1Async(COSGeniusSystem.DTOs.Compatibility.V1.CreatePatientRequest request, CancellationToken ct = default);
    Task<PatientDetailDto> UpdatePatientV1Async(int id, COSGeniusSystem.DTOs.Compatibility.V1.UpdatePatientRequest request, CancellationToken ct = default);
    Task<List<PatientVisitHistoryDto>> GetVisitHistoryAsync(int patientId, CancellationToken ct = default);
    Task<MedicalRecordDetailDto> GetMedicalRecordDetailAsync(int recordId, CancellationToken ct = default);
    Task<List<TreatmentProtocolDto>> GetTreatmentProtocolsAsync(int patientId, CancellationToken ct = default);
    Task<List<TreatmentSessionDto>> GetTreatmentSessionsAsync(int planId, CancellationToken ct = default);
}

using COSGeniusSystem.Commons;
using COSGeniusSystem.DTOs.Requests;
using COSGeniusSystem.DTOs.Responses;

namespace COSGeniusSystem.Services.Interfaces;

public interface IPatientService
{
    Task<PagedResult<PatientResponse>> GetAllAsync(int pageNumber, int pageSize, CancellationToken ct);
    Task<PatientResponse> GetByIdAsync(int patientId, CancellationToken ct);
    Task<PatientResponse> CreateAsync(CreatePatientRequest request, CancellationToken ct);
    Task UpdateAsync(int patientId, UpdatePatientRequest request, CancellationToken ct);
    Task DeleteAsync(int patientId, CancellationToken ct);
}

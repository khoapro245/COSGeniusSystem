using COSGeniusSystem.Commons;
using COSGeniusSystem.DTOs.Requests;
using COSGeniusSystem.DTOs.Responses;

namespace COSGeniusSystem.Services.Interfaces;

public interface IPatientContactService
{
    Task<PagedResult<PatientContactResponse>> GetAllAsync(int patientId, int pageNumber, int pageSize, CancellationToken ct);
    Task<PatientContactResponse> GetByIdAsync(int patientId, int contactId, CancellationToken ct);
    Task<PatientContactResponse> CreateAsync(int patientId, CreatePatientContactRequest request, CancellationToken ct);
    Task UpdateAsync(int patientId, int contactId, UpdatePatientContactRequest request, CancellationToken ct);
    Task DeleteAsync(int patientId, int contactId, CancellationToken ct);
}

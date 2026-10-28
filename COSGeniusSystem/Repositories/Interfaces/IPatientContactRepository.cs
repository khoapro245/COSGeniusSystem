using COSGeniusSystem.Models;

namespace COSGeniusSystem.Repositories.Interfaces;

public interface IPatientContactRepository
{
    Task<IReadOnlyList<PatientContact>> GetAllAsync(int patientId, int pageNumber, int pageSize, CancellationToken ct);
    Task<PatientContact?> GetByIdAsync(int patientId, int contactId, CancellationToken ct);
    Task<int> CountAsync(int patientId, CancellationToken ct);
    Task AddAsync(PatientContact entity, CancellationToken ct);
    void Remove(PatientContact entity);
    Task SaveChangesAsync(CancellationToken ct);
}

using COSGeniusSystem.Models;

namespace COSGeniusSystem.Repositories.Interfaces;

public interface IPatientRepository
{
    Task<IReadOnlyList<Patient>> GetAllAsync(int pageNumber, int pageSize, CancellationToken ct);
    Task<Patient?> GetByIdAsync(int patientId, CancellationToken ct);
    Task<bool> ExistsAsync(int patientId, CancellationToken ct);
    Task<bool> HasDependentsAsync(int patientId, CancellationToken ct);
    Task<int> CountAsync(CancellationToken ct);
    Task AddAsync(Patient entity, CancellationToken ct);
    void Remove(Patient entity);
    Task SaveChangesAsync(CancellationToken ct);
}

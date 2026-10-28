using COSGeniusSystem.Data;
using COSGeniusSystem.Models;
using COSGeniusSystem.Repositories.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace COSGeniusSystem.Repositories;

public sealed class PatientRepository(ApplicationDbContext db) : IPatientRepository
{
    public async Task<IReadOnlyList<Patient>> GetAllAsync(int pageNumber, int pageSize, CancellationToken ct) =>
        await db.Patients.AsNoTracking().OrderBy(p => p.PatientId).Skip((pageNumber - 1) * pageSize).Take(pageSize).ToListAsync(ct);

    public Task<Patient?> GetByIdAsync(int patientId, CancellationToken ct) =>
        db.Patients.SingleOrDefaultAsync(p => p.PatientId == patientId, ct);

    public Task<bool> ExistsAsync(int patientId, CancellationToken ct) => db.Patients.AnyAsync(p => p.PatientId == patientId, ct);

    public async Task<bool> HasDependentsAsync(int patientId, CancellationToken ct) =>
        await db.PatientContacts.AnyAsync(p => p.PatientId == patientId, ct) || await db.MedicalRecords.AnyAsync(p => p.PatientId == patientId, ct);

    public Task<int> CountAsync(CancellationToken ct) => db.Patients.CountAsync(ct);

    public async Task AddAsync(Patient entity, CancellationToken ct) => await db.Patients.AddAsync(entity, ct);
    public void Remove(Patient entity) => db.Patients.Remove(entity);
    public async Task SaveChangesAsync(CancellationToken ct) => await db.SaveChangesAsync(ct);
}

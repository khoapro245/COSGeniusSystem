using COSGeniusSystem.Data;
using COSGeniusSystem.Models;
using COSGeniusSystem.Repositories.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace COSGeniusSystem.Repositories;

public sealed class PatientContactRepository(ApplicationDbContext db) : IPatientContactRepository
{
    public async Task<IReadOnlyList<PatientContact>> GetAllAsync(int patientId, int pageNumber, int pageSize, CancellationToken ct) =>
        await db.PatientContacts.AsNoTracking().Where(p => p.PatientId == patientId).OrderBy(p => p.ContactId).Skip((pageNumber - 1) * pageSize).Take(pageSize).ToListAsync(ct);

    public Task<PatientContact?> GetByIdAsync(int patientId, int contactId, CancellationToken ct) =>
        db.PatientContacts.SingleOrDefaultAsync(p => p.PatientId == patientId && p.ContactId == contactId, ct);

    public Task<int> CountAsync(int patientId, CancellationToken ct) => db.PatientContacts.CountAsync(p => p.PatientId == patientId, ct);

    public async Task AddAsync(PatientContact entity, CancellationToken ct) => await db.PatientContacts.AddAsync(entity, ct);
    public void Remove(PatientContact entity) => db.PatientContacts.Remove(entity);
    public async Task SaveChangesAsync(CancellationToken ct) => await db.SaveChangesAsync(ct);
}

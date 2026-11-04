using COSGeniusSystem.Commons;
using COSGeniusSystem.Exceptions;
using COSGeniusSystem.Models;
using COSGeniusSystem.Data;
using COSGeniusSystem.Services.Interfaces;
using Microsoft.EntityFrameworkCore;
using COSGeniusSystem.DTOs.Patients.Requests;
using COSGeniusSystem.DTOs.Patients.Responses;

namespace COSGeniusSystem.Services;

public sealed class PatientContactService(ApplicationDbContext db) : IPatientContactService
{
    private async Task EnsurePatientAsync(int patientId, CancellationToken ct)
    {
        if (!await db.Patients.AnyAsync(p => p.PatientId == patientId, ct)) throw new NotFoundException("Patient not found.");
    }

    private async Task<PatientContact> FindAsync(int patientId, int contactId, CancellationToken ct)
    {
        await EnsurePatientAsync(patientId, ct);
        return await db.PatientContacts.SingleOrDefaultAsync(p => p.PatientId == patientId && p.ContactId == contactId, ct) ?? throw new NotFoundException("Contact not found for this patient.");
    }

    public async Task<PagedResult<PatientContactResponse>> GetAllAsync(int patientId, int pageNumber, int pageSize, CancellationToken ct)
    {
        if (pageNumber < 1 || pageNumber > 1000000 || pageSize < 1 || pageSize > 100)
            throw new BadRequestException("Page number must be 1 to 1000000 and page size must be 1 to 100.");
        await EnsurePatientAsync(patientId, ct);
        var totalCount = await db.PatientContacts.CountAsync(p => p.PatientId == patientId, ct);
        var items = (await db.PatientContacts.AsNoTracking().Where(p => p.PatientId == patientId)
            .OrderBy(p => p.ContactId).Skip((pageNumber - 1) * pageSize).Take(pageSize).ToListAsync(ct))
            .Select(PatientContactMapping.ToResponse).ToList();
        return new(items, pageNumber, pageSize, totalCount);
    }

    public async Task<PatientContactResponse> GetByIdAsync(int patientId, int contactId, CancellationToken ct) =>
        PatientContactMapping.ToResponse(await FindAsync(patientId, contactId, ct));

    public async Task<PatientContactResponse> CreateAsync(int patientId, CreatePatientContactRequest request, CancellationToken ct)
    {
        await EnsurePatientAsync(patientId, ct);
        var entity = new PatientContact { PatientId = patientId };
        PatientContactMapping.Apply(request, entity);
        await db.PatientContacts.AddAsync(entity, ct);
        await db.SaveChangesAsync(ct);
        return PatientContactMapping.ToResponse(entity);
    }

    public async Task UpdateAsync(int patientId, int contactId, UpdatePatientContactRequest request, CancellationToken ct)
    {
        var entity = await FindAsync(patientId, contactId, ct);
        PatientContactMapping.Apply(request, entity);
        await db.SaveChangesAsync(ct);
    }

    public async Task DeleteAsync(int patientId, int contactId, CancellationToken ct)
    {
        var entity = await FindAsync(patientId, contactId, ct);
        db.PatientContacts.Remove(entity);
        await db.SaveChangesAsync(ct);
    }
}

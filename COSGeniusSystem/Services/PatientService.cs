using COSGeniusSystem.Commons;
using COSGeniusSystem.DTOs.Requests;
using COSGeniusSystem.DTOs.Responses;
using COSGeniusSystem.Exceptions;
using COSGeniusSystem.Models;
using COSGeniusSystem.Data;
using COSGeniusSystem.Services.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace COSGeniusSystem.Services;

public sealed class PatientService(ApplicationDbContext db) : IPatientService
{
    private async Task<Patient> FindAsync(int patientId, CancellationToken ct)
    {
        return await db.Patients.SingleOrDefaultAsync(p => p.PatientId == patientId, ct) ?? throw new NotFoundException("Patient not found.");
    }

    public async Task<PagedResult<PatientResponse>> GetAllAsync(int pageNumber, int pageSize, CancellationToken ct)
    {
        if (pageNumber < 1 || pageNumber > 1000000 || pageSize < 1 || pageSize > 100)
            throw new BadRequestException("Page number must be 1 to 1000000 and page size must be 1 to 100.");
        var totalCount = await db.Patients.CountAsync(ct);
        var items = (await db.Patients.AsNoTracking().OrderBy(p => p.PatientId)
            .Skip((pageNumber - 1) * pageSize).Take(pageSize).ToListAsync(ct))
            .Select(PatientMapping.ToResponse).ToList();
        return new(items, pageNumber, pageSize, totalCount);
    }

    public async Task<PatientResponse> GetByIdAsync(int patientId, CancellationToken ct) =>
        PatientMapping.ToResponse(await FindAsync(patientId, ct));

    public async Task<PatientResponse> CreateAsync(CreatePatientRequest request, CancellationToken ct)
    {
        Validate(request);
        var entity = new Patient();
        PatientMapping.Apply(request, entity);
        await db.Patients.AddAsync(entity, ct);
        await db.SaveChangesAsync(ct);
        return PatientMapping.ToResponse(entity);
    }

    public async Task UpdateAsync(int patientId, UpdatePatientRequest request, CancellationToken ct)
    {
        Validate(request);
        var entity = await FindAsync(patientId, ct);
        PatientMapping.Apply(request, entity);
        await db.SaveChangesAsync(ct);
    }

    public async Task DeleteAsync(int patientId, CancellationToken ct)
    {
        var entity = await FindAsync(patientId, ct);
        if (await db.PatientContacts.AnyAsync(p => p.PatientId == patientId, ct)
            || await db.MedicalRecords.AnyAsync(p => p.PatientId == patientId, ct))
            throw new ConflictException("Patient has contacts or medical records. Remove related records before deleting the patient.");
        db.Patients.Remove(entity);
        await db.SaveChangesAsync(ct);
    }

    private static void Validate(PatientRequest request)
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        if (request.DateOfBirth > today || request.IdentityCardIssuedDate > today)
            throw new BadRequestException("Birth date and identity card issue date cannot be in the future.");
        if (request.IdentityCardIssuedDate < request.DateOfBirth)
            throw new BadRequestException("Identity card issue date cannot precede birth date.");
    }
}

using COSGeniusSystem.Commons;
using COSGeniusSystem.DTOs.Requests;
using COSGeniusSystem.DTOs.Responses;
using COSGeniusSystem.Exceptions;
using COSGeniusSystem.Models;
using COSGeniusSystem.Repositories.Interfaces;
using COSGeniusSystem.Services.Interfaces;

namespace COSGeniusSystem.Services;

public sealed class PatientContactService(IPatientContactRepository repository, IPatientRepository patients) : IPatientContactService
{
    private async Task EnsurePatientAsync(int patientId, CancellationToken ct)
    {
        if (!await patients.ExistsAsync(patientId, ct)) throw new NotFoundException("Patient not found.");
    }

    private async Task<PatientContact> FindAsync(int patientId, int contactId, CancellationToken ct)
    {
        await EnsurePatientAsync(patientId, ct);
        return await repository.GetByIdAsync(patientId, contactId, ct) ?? throw new NotFoundException("Contact not found for this patient.");
    }

    public async Task<PagedResult<PatientContactResponse>> GetAllAsync(int patientId, int pageNumber, int pageSize, CancellationToken ct)
    {
        if (pageNumber < 1 || pageNumber > 1000000 || pageSize < 1 || pageSize > 100)
            throw new BadRequestException("Page number must be 1 to 1000000 and page size must be 1 to 100.");
        await EnsurePatientAsync(patientId, ct);
        var totalCount = await repository.CountAsync(patientId, ct);
        var items = (await repository.GetAllAsync(patientId, pageNumber, pageSize, ct)).Select(PatientContactMapping.ToResponse).ToList();
        return new(items, pageNumber, pageSize, totalCount);
    }

    public async Task<PatientContactResponse> GetByIdAsync(int patientId, int contactId, CancellationToken ct) =>
        PatientContactMapping.ToResponse(await FindAsync(patientId, contactId, ct));

    public async Task<PatientContactResponse> CreateAsync(int patientId, CreatePatientContactRequest request, CancellationToken ct)
    {
        await EnsurePatientAsync(patientId, ct);
        var entity = new PatientContact { PatientId = patientId };
        PatientContactMapping.Apply(request, entity);
        await repository.AddAsync(entity, ct);
        await repository.SaveChangesAsync(ct);
        return PatientContactMapping.ToResponse(entity);
    }

    public async Task UpdateAsync(int patientId, int contactId, UpdatePatientContactRequest request, CancellationToken ct)
    {
        var entity = await FindAsync(patientId, contactId, ct);
        PatientContactMapping.Apply(request, entity);
        await repository.SaveChangesAsync(ct);
    }

    public async Task DeleteAsync(int patientId, int contactId, CancellationToken ct)
    {
        var entity = await FindAsync(patientId, contactId, ct);
        repository.Remove(entity);
        await repository.SaveChangesAsync(ct);
    }
}

using COSGeniusSystem.Data;
using COSGeniusSystem.DTOs.TreatmentServices;
using COSGeniusSystem.Models;
using Microsoft.EntityFrameworkCore;

namespace COSGeniusSystem.Services.TreatmentServices;

public class TreatmentService : ITreatmentService
{
    private readonly ApplicationDbContext _context;

    public TreatmentService(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<List<TreatmentServiceResponseDto>> GetActiveServicesAsync(CancellationToken ct = default)
    {
        return await _context.Services
            .AsNoTracking()
            .Where(s => s.IsActive)
            .Select(s => new TreatmentServiceResponseDto
            {
                ServiceId = s.ServiceId, ServiceCode = s.ServiceCode, ServiceName = s.ServiceName,
                Price = s.Price, Unit = s.Unit, Description = s.Description, IsActive = s.IsActive
            })
            .ToListAsync(ct);
    }

    public async Task<(TreatmentServiceResponseDto? Dto, string? ErrorMessage)> CreateServiceAsync(
        CreateTreatmentServiceRequest request)
    {
        // Validation: Price phải > 0
        if (request.Price <= 0)
            return (null, "Price must be greater than 0.");

        // Kiểm tra trùng ServiceCode hoặc ServiceName
        var isDuplicate = await _context.Services.AnyAsync(s =>
            s.ServiceCode == request.ServiceCode ||
            s.ServiceName == request.ServiceName);

        if (isDuplicate)
            return (null, "ServiceCode or ServiceName already exists.");

        var entity = new Service
        {
            ServiceCode = request.ServiceCode,
            ServiceName = request.ServiceName,
            Price = request.Price,
            Unit = request.Unit,
            Description = request.Description,
            IsActive = true
        };

        _context.Services.Add(entity);
        await _context.SaveChangesAsync();

        return (ToResponseDto(entity), null);
    }

    public async Task<(TreatmentServiceResponseDto? Dto, string? ErrorMessage)> UpdateServiceAsync(
        int id, UpdateTreatmentServiceRequest request)
    {
        var entity = await _context.Services.FindAsync(id);
        if (entity is null)
            return (null, null); // null Dto → 404 ở Controller

        // Validation: Price phải > 0
        if (request.Price <= 0)
            return (null, "Price must be greater than 0.");

        // Kiểm tra trùng ServiceName với dịch vụ khác (khác id hiện tại)
        var isDuplicateName = await _context.Services.AnyAsync(s =>
            s.ServiceName == request.ServiceName &&
            s.ServiceId != id);

        if (isDuplicateName)
            return (null, $"ServiceName '{request.ServiceName}' already exists in another service.");

        entity.ServiceName = request.ServiceName;
        entity.Price = request.Price;
        entity.Unit = request.Unit;
        entity.Description = request.Description;
        entity.IsActive = request.IsActive;

        await _context.SaveChangesAsync();

        return (ToResponseDto(entity), null);
    }

    // ---------- Helper ----------
    private static TreatmentServiceResponseDto ToResponseDto(Service s) => new()
    {
        ServiceId = s.ServiceId,
        ServiceCode = s.ServiceCode,
        ServiceName = s.ServiceName,
        Price = s.Price,
        Unit = s.Unit,
        Description = s.Description,
        IsActive = s.IsActive
    };
}

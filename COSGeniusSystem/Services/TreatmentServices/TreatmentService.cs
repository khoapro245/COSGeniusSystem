using COSGeniusSystem.Data;
using COSGeniusSystem.DTOs;
using COSGeniusSystem.Models;
using Microsoft.EntityFrameworkCore;

namespace COSGeniusSystem.Services;

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
            .OrderBy(s => s.ServiceCode)
            .Select(s => ToResponseDto(s))
            .ToListAsync(ct);
    }

    public async Task<List<TreatmentServiceResponseDto>> GetServicesAsync(TreatmentServiceFilterQuery query, CancellationToken ct = default)
    {
        var q = _context.Services.AsNoTracking();

        if (query.IsActive.HasValue)
        {
            q = q.Where(s => s.IsActive == query.IsActive.Value);
        }
        else if (query.IncludeInactive != true)
        {
            q = q.Where(s => s.IsActive);
        }

        if (!string.IsNullOrWhiteSpace(query.Keyword))
        {
            var kw = query.Keyword.Trim().ToLower();
            q = q.Where(s =>
                s.ServiceCode.ToLower().Contains(kw) ||
                s.ServiceName.ToLower().Contains(kw) ||
                (s.Description != null && s.Description.ToLower().Contains(kw)));
        }

        var page = query.Page < 1 ? 1 : query.Page;
        var pageSize = query.PageSize < 1 ? 10 : Math.Min(query.PageSize, 100);

        return await q
            .OrderBy(s => s.ServiceCode)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(s => ToResponseDto(s))
            .ToListAsync(ct);
    }

    public async Task<TreatmentServiceResponseDto?> GetServiceByIdAsync(int id, CancellationToken ct = default)
    {
        var entity = await _context.Services
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.ServiceId == id, ct);

        return entity is null ? null : ToResponseDto(entity);
    }

    public async Task<TreatmentServiceResponseDto?> GetServiceByCodeAsync(string code, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(code)) return null;

        var cleanCode = code.Trim().ToLower();
        var entity = await _context.Services
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.ServiceCode.ToLower() == cleanCode, ct);

        return entity is null ? null : ToResponseDto(entity);
    }

    public async Task<(TreatmentServiceResponseDto? Dto, string? ErrorMessage)> CreateServiceAsync(
        CreateTreatmentServiceRequest request, CancellationToken ct = default)
    {
        // Validation: Price phải > 0
        if (request.Price <= 0)
            return (null, "Price must be greater than 0.");

        string serviceCode;
        if (string.IsNullOrWhiteSpace(request.ServiceCode))
        {
            // Tự sinh mã dạng DV00001, DV00002...
            var count = await _context.Services.CountAsync(ct);
            var candidateNum = count + 1;
            serviceCode = $"DV{candidateNum:D5}";

            while (await _context.Services.AnyAsync(s => s.ServiceCode == serviceCode, ct))
            {
                candidateNum++;
                serviceCode = $"DV{candidateNum:D5}";
            }
        }
        else
        {
            serviceCode = request.ServiceCode.Trim().ToUpperInvariant();
            var isDuplicateCode = await _context.Services.AnyAsync(s =>
                s.ServiceCode.ToLower() == serviceCode.ToLower(), ct);

            if (isDuplicateCode)
                return (null, "ServiceCode already exists.");
        }

        var isDuplicateName = await _context.Services.AnyAsync(s =>
            s.ServiceName.ToLower() == request.ServiceName.Trim().ToLower(), ct);

        if (isDuplicateName)
            return (null, "ServiceName already exists.");

        var entity = new Service
        {
            ServiceCode = serviceCode,
            ServiceName = request.ServiceName.Trim(),
            Price = request.Price,
            Unit = string.IsNullOrWhiteSpace(request.Unit) ? "Buổi" : request.Unit.Trim(),
            Description = string.IsNullOrWhiteSpace(request.Description) ? null : request.Description.Trim(),
            IsActive = request.IsActive
        };

        _context.Services.Add(entity);
        await _context.SaveChangesAsync(ct);

        return (ToResponseDto(entity), null);
    }

    public async Task<(TreatmentServiceResponseDto? Dto, string? ErrorMessage)> UpdateServiceAsync(
        int id, UpdateTreatmentServiceRequest request, CancellationToken ct = default)
    {
        var entity = await _context.Services.FirstOrDefaultAsync(s => s.ServiceId == id, ct);
        if (entity is null)
            return (null, null); // null Dto → 404 ở Controller

        // Validation: Price phải > 0
        if (request.Price <= 0)
            return (null, "Price must be greater than 0.");

        // Kiểm tra trùng ServiceName với dịch vụ khác (khác id hiện tại)
        var isDuplicateName = await _context.Services.AnyAsync(s =>
            s.ServiceName.ToLower() == request.ServiceName.Trim().ToLower() &&
            s.ServiceId != id, ct);

        if (isDuplicateName)
            return (null, $"ServiceName '{request.ServiceName}' already exists in another service.");

        entity.ServiceName = request.ServiceName.Trim();
        entity.Price = request.Price;
        if (!string.IsNullOrWhiteSpace(request.Unit))
            entity.Unit = request.Unit.Trim();
        entity.Description = string.IsNullOrWhiteSpace(request.Description) ? null : request.Description.Trim();
        entity.IsActive = request.IsActive;

        await _context.SaveChangesAsync(ct);

        return (ToResponseDto(entity), null);
    }

    public async Task<(bool Success, bool IsDeactivated, string? ErrorMessage)> DeleteServiceAsync(int id, CancellationToken ct = default)
    {
        var entity = await _context.Services
            .Include(s => s.TreatmentPlanDetails)
            .FirstOrDefaultAsync(s => s.ServiceId == id, ct);

        if (entity is null)
            return (false, false, "Không tìm thấy dịch vụ điều trị.");

        var isReferenced = entity.TreatmentPlanDetails.Any();

        if (isReferenced)
        {
            // Đã có hồ sơ điều trị liên kết: ngưng hoạt động để bảo đảm toàn vẹn lịch sử
            entity.IsActive = false;
            await _context.SaveChangesAsync(ct);
            return (true, true, null);
        }

        // Chưa có dữ liệu liên kết: xóa cứng khỏi CSDL
        _context.Services.Remove(entity);
        await _context.SaveChangesAsync(ct);
        return (true, false, null);
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

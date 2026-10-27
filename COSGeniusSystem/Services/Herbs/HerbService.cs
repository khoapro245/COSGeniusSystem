using COSGeniusSystem.Data;
using COSGeniusSystem.DTOs.Herbs;
using COSGeniusSystem.Models;
using Microsoft.EntityFrameworkCore;

namespace COSGeniusSystem.Services.Herbs;

public class HerbService : IHerbService
{
    private readonly ApplicationDbContext _context;

    public HerbService(ApplicationDbContext context)
    {
        _context = context;
    }

    // ─── API-TU-01 ────────────────────────────────────────────────────────────
    public async Task<List<HerbResponseDto>> GetHerbsAsync(HerbFilterQuery query)
    {
        var q = _context.Herbs.AsNoTracking();

        // Lọc IsActive (mặc định true)
        if (query.IsActive.HasValue)
            q = q.Where(h => h.IsActive == query.IsActive.Value);

        // Lọc theo TasteProperties (tính chất / quy kinh)
        if (!string.IsNullOrWhiteSpace(query.TasteProperties))
            q = q.Where(h => h.TasteProperties != null
                && h.TasteProperties.ToLower().Contains(query.TasteProperties.ToLower()));

        // Tìm kiếm tương đối theo CommonName hoặc HerbCode
        if (!string.IsNullOrWhiteSpace(query.Keyword))
        {
            var keyword = query.Keyword.ToLower();
            q = q.Where(h =>
                h.CommonName.ToLower().Contains(keyword) ||
                h.HerbCode.ToLower().Contains(keyword));
        }

        // Phân trang
        var page = query.Page < 1 ? 1 : query.Page;
        var pageSize = query.PageSize < 1 ? 20 : query.PageSize;

        return await q
            .OrderBy(h => h.CommonName)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(h => ToResponseDto(h))
            .ToListAsync();
    }

    // ─── API-TU-02 ────────────────────────────────────────────────────────────
    public async Task<HerbDetailDto?> GetHerbByIdAsync(int id)
    {
        var herb = await _context.Herbs
            .AsNoTracking()
            .FirstOrDefaultAsync(h => h.HerbId == id);

        if (herb is null)
            return null;

        return new HerbDetailDto
        {
            HerbId = herb.HerbId,
            HerbCode = herb.HerbCode,
            CommonName = herb.CommonName,
            ScientificName = herb.ScientificName,
            Unit = herb.Unit,
            TasteProperties = herb.TasteProperties,
            StandardPrice = herb.StandardPrice,
            MinThreshold = herb.MinThreshold,
            IsActive = herb.IsActive
        };
    }

    // ─── API-TU-03 ────────────────────────────────────────────────────────────
    public async Task<(HerbResponseDto? Dto, string? ErrorMessage)> CreateHerbAsync(
        CreateHerbRequest request)
    {
        // Validation: StandardPrice phải > 0
        if (request.StandardPrice <= 0)
            return (null, "StandardPrice must be greater than 0.");

        // Kiểm tra trùng HerbCode hoặc CommonName
        var isDuplicate = await _context.Herbs.AnyAsync(h =>
            h.HerbCode == request.HerbCode ||
            h.CommonName == request.CommonName);

        if (isDuplicate)
            return (null, "HerbCode or CommonName already exists.");

        var entity = new Herb
        {
            HerbCode = request.HerbCode,
            CommonName = request.CommonName,
            ScientificName = request.ScientificName,
            Unit = request.Unit,
            TasteProperties = request.TasteProperties,
            StandardPrice = request.StandardPrice,
            MinThreshold = request.MinThreshold,
            IsActive = true
        };

        _context.Herbs.Add(entity);
        await _context.SaveChangesAsync();

        return (ToResponseDto(entity), null);
    }

    // ─── API-TU-04 ────────────────────────────────────────────────────────────
    public async Task<(HerbResponseDto? Dto, string? ErrorMessage)> UpdateHerbAsync(
        int id, UpdateHerbRequest request)
    {
        var entity = await _context.Herbs.FindAsync(id);
        if (entity is null)
            return (null, null); // null Dto → 404 ở Controller

        // Validation: StandardPrice phải > 0
        if (request.StandardPrice <= 0)
            return (null, "StandardPrice must be greater than 0.");

        // Kiểm tra trùng CommonName với vị thuốc khác (khác id hiện tại)
        var isDuplicateName = await _context.Herbs.AnyAsync(h =>
            h.CommonName == request.CommonName &&
            h.HerbId != id);

        if (isDuplicateName)
            return (null, $"CommonName '{request.CommonName}' already exists in another herb.");

        entity.CommonName = request.CommonName;
        entity.ScientificName = request.ScientificName;
        entity.Unit = request.Unit;
        entity.TasteProperties = request.TasteProperties;
        entity.StandardPrice = request.StandardPrice;
        entity.MinThreshold = request.MinThreshold;
        entity.IsActive = request.IsActive;

        await _context.SaveChangesAsync();

        return (ToResponseDto(entity), null);
    }

    // ─── API-TU-05 ────────────────────────────────────────────────────────────
    public async Task<HerbBatchesResponseDto?> GetHerbBatchesAsync(int herbId)
    {
        var herb = await _context.Herbs
            .AsNoTracking()
            .FirstOrDefaultAsync(h => h.HerbId == herbId);

        if (herb is null)
            return null;

        // Lấy các lô kèm thông tin kho, sắp xếp theo FEFO (ExpiryDate tăng dần)
        var batches = await _context.Batches
            .AsNoTracking()
            .Include(b => b.Warehouse)
            .Where(b => b.HerbId == herbId)
            .OrderBy(b => b.ExpiryDate)   // FEFO
            .ThenBy(b => b.ImportDate)    // FIFO trong cùng ngày hết hạn
            .Select(b => new BatchResponseDto
            {
                BatchId = b.BatchId,
                BatchNumber = b.BatchNumber,
                WarehouseId = b.WarehouseId,
                WarehouseName = b.Warehouse.WarehouseName,
                ImportDate = b.ImportDate,
                ExpiryDate = b.ExpiryDate,
                OriginalQuantity = b.OriginalQuantity,
                CurrentQuantity = b.CurrentQuantity,
                UnitCost = b.UnitCost,
                Status = b.Status
            })
            .ToListAsync();

        return new HerbBatchesResponseDto
        {
            HerbId = herb.HerbId,
            HerbCode = herb.HerbCode,
            CommonName = herb.CommonName,
            Unit = herb.Unit,
            TotalCurrentQuantity = batches.Sum(b => b.CurrentQuantity),
            Batches = batches
        };
    }

    // ─── Helper ───────────────────────────────────────────────────────────────
    private static HerbResponseDto ToResponseDto(Herb h) => new()
    {
        HerbId = h.HerbId,
        HerbCode = h.HerbCode,
        CommonName = h.CommonName,
        ScientificName = h.ScientificName,
        Unit = h.Unit,
        TasteProperties = h.TasteProperties,
        StandardPrice = h.StandardPrice,
        IsActive = h.IsActive
    };
}

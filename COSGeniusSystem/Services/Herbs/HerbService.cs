using COSGeniusSystem.Data;
using COSGeniusSystem.DTOs;
using COSGeniusSystem.Models;
using Microsoft.EntityFrameworkCore;

namespace COSGeniusSystem.Services;

public class HerbService : IHerbService
{
    private readonly ApplicationDbContext _context;

    public HerbService(ApplicationDbContext context)
    {
        _context = context;
    }

    // ─── API-TU-01 ────────────────────────────────────────────────────────────
    public async Task<List<HerbResponseDto>> GetHerbsAsync(HerbFilterQuery query, CancellationToken ct = default)
    {
        var q = _context.Herbs.AsNoTracking();

        // Lọc IsActive (mặc định true nếu client không truyền)
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
        var pageSize = query.PageSize < 1 ? 10 : query.PageSize;

        return await q
            .OrderBy(h => h.CommonName)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(h => new HerbResponseDto
            {
                HerbId = h.HerbId,
                HerbCode = h.HerbCode,
                CommonName = h.CommonName,
                ScientificName = h.ScientificName,
                Unit = h.Unit,
                TasteProperties = h.TasteProperties,
                StandardPrice = h.StandardPrice,
                IsActive = h.IsActive
            })
            .ToListAsync(ct);
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
        // Changing this label would reinterpret existing batch balances and history.
        if (entity.Unit != request.Unit && await _context.Batches.AnyAsync(b => b.HerbId == id))
            return (null, "Không thể đổi đơn vị khi vị thuốc đã có lô. Cần quy trình chuyển đổi dữ liệu có đối soát.");
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
        var batchEntities = await _context.Batches
            .AsNoTracking()
            .Include(b => b.Warehouse)
            .Where(b => b.HerbId == herbId)
            .OrderBy(b => b.ExpiryDate)   // FEFO
            .ThenBy(b => b.ImportDate)    // FIFO trong cùng ngày hết hạn
            .ToListAsync();

        var auditedBatches = await _context.AuditDetails
            .AsNoTracking()
            .Include(ad => ad.Audit)
            .Where(ad => ad.Audit.Status == "Đã duyệt" && ad.Batch.HerbId == herbId)
            .GroupBy(ad => ad.BatchId)
            .Select(g => new
            {
                BatchId = g.Key,
                LatestAuditDate = g.OrderByDescending(x => x.AuditId).First().Audit.AuditDate,
                SystemQuantity = g.OrderBy(x => x.AuditId).First().SystemQuantity,
                LatestReason = g.OrderByDescending(x => x.AuditId).First().Reason
            })
            .ToListAsync();

        var batchSysMap = auditedBatches.ToDictionary(x => x.BatchId, x => x.SystemQuantity);
        var auditDateMap = auditedBatches.ToDictionary(x => x.BatchId, x => x.LatestAuditDate);
        var batchDestroyMap = new Dictionary<int, decimal>();
        foreach (var item in auditedBatches)
        {
            if (!string.IsNullOrEmpty(item.LatestReason))
            {
                var match = System.Text.RegularExpressions.Regex.Match(item.LatestReason, @"\[Tiêu hủy:\s*([\d\.]+)\s*(?:kg|g)?\]");
                if (match.Success && decimal.TryParse(match.Groups[1].Value, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out var qty))
                {
                    batchDestroyMap[item.BatchId] = qty;
                }
            }
        }

        var completedDisposalsList = await _context.DisposalDetails
            .Include(dd => dd.Disposal)
            .Where(dd => dd.Disposal.Status == "Hoàn thành" || dd.Disposal.Status == "Đã tiêu hủy")
            .Select(dd => new { dd.BatchId, dd.DisposalQuantity, dd.Disposal.DisposalDate })
            .ToListAsync();

        var completedDisposals = completedDisposalsList
            .Where(dd => !auditDateMap.TryGetValue(dd.BatchId, out var aDate) || dd.DisposalDate >= aDate.AddMinutes(-5))
            .GroupBy(dd => dd.BatchId)
            .ToDictionary(g => g.Key, g => g.Sum(x => x.DisposalQuantity));

        var pendingDisposals = await _context.DisposalDetails
            .Include(dd => dd.Disposal)
            .Where(dd => (dd.Disposal.Status == "Chờ quản lý duyệt" || dd.Disposal.Status == "Đã duyệt / Chờ hủy" || dd.Disposal.Status == "Đã duyệt")
                      && dd.Batch.HerbId == herbId)
            .GroupBy(dd => dd.BatchId)
            .ToDictionaryAsync(g => g.Key, g => g.Sum(x => x.DisposalQuantity));

        var batches = batchEntities.Select(b =>
        {
            var rawDestrQty = batchDestroyMap.TryGetValue(b.BatchId, out var dQty) ? dQty : 0m;
            var compQty = completedDisposals.TryGetValue(b.BatchId, out var cQty) ? cQty : 0m;
            var auditPendingDestr = Math.Max(0m, rawDestrQty - compQty);
            var directPendingDestr = pendingDisposals.TryGetValue(b.BatchId, out var pQty) ? pQty : 0m;
            var destrQty = Math.Max(auditPendingDestr, directPendingDestr);
            var usableQty = Math.Max(0m, b.CurrentQuantity - destrQty);
            return new BatchResponseDto
            {
                BatchId = b.BatchId,
                BatchNumber = b.BatchNumber,
                WarehouseId = b.WarehouseId,
                WarehouseName = b.Warehouse?.WarehouseName ?? "Kho Dược liệu trung tâm",
                ImportDate = b.ImportDate,
                ExpiryDate = b.ExpiryDate,
                OriginalQuantity = b.OriginalQuantity,
                CurrentQuantity = b.CurrentQuantity,
                SystemQuantity = batchSysMap.TryGetValue(b.BatchId, out var sysQty) ? sysQty : b.CurrentQuantity,
                UsableQuantity = usableQty,
                DestroyQuantity = destrQty,
                UnitCost = b.UnitCost,
                Status = b.Status
            };
        }).ToList();

        return new HerbBatchesResponseDto
        {
            HerbId = herb.HerbId,
            HerbCode = herb.HerbCode,
            CommonName = herb.CommonName,
            Unit = herb.Unit,
            TotalSystemQuantity = batches.Sum(b => b.SystemQuantity),
            TotalCurrentQuantity = batches.Sum(b => b.CurrentQuantity),
            Batches = batches
        };
    }

    // ─── Delete ───────────────────────────────────────────────────────────────
    public async Task<(bool Success, string? ErrorMessage)> DeleteHerbAsync(int id, CancellationToken ct = default)
    {
        var entity = await _context.Herbs
            .Include(h => h.Batches)
            .Include(h => h.HerbalPrescriptionDetails)
            .Include(h => h.Podetails)
            .Include(h => h.HerbalFormulaDetails)
            .FirstOrDefaultAsync(h => h.HerbId == id, ct);

        if (entity is null)
            return (false, null);

        // Nếu đã có lô hàng hoặc được tham chiếu trong đơn thuốc / PO / công thức -> ngưng hoạt động (soft delete)
        if (entity.Batches.Any() || entity.HerbalPrescriptionDetails.Any() || entity.Podetails.Any() || entity.HerbalFormulaDetails.Any())
        {
            entity.IsActive = false;
        }
        else
        {
            _context.Herbs.Remove(entity);
        }

        await _context.SaveChangesAsync(ct);
        return (true, null);
    }

    // ─── API-TU-06: Inventory Summary ──────────────────────────────────────────
    public async Task<List<HerbInventoryItemDto>> GetHerbInventorySummaryAsync(CancellationToken ct = default)
    {
        var herbs = await _context.Herbs
            .AsNoTracking()
            .Include(h => h.Batches)
                .ThenInclude(b => b.Warehouse)
            .OrderBy(h => h.CommonName)
            .ToListAsync(ct);

        // Lấy số tồn sổ sách hệ thống ban đầu và thông tin tiêu hủy của các lô đã kiểm kê
        var auditedBatches = await _context.AuditDetails
            .AsNoTracking()
            .Include(ad => ad.Audit)
            .Where(ad => ad.Audit.Status == "Đã duyệt")
            .GroupBy(ad => ad.BatchId)
            .Select(g => new
            {
                BatchId = g.Key,
                LatestAuditDate = g.OrderByDescending(x => x.AuditId).First().Audit.AuditDate,
                SystemQuantity = g.OrderBy(x => x.AuditId).First().SystemQuantity,
                LatestReason = g.OrderByDescending(x => x.AuditId).First().Reason
            })
            .ToListAsync(ct);

        var batchSysMap = auditedBatches.ToDictionary(x => x.BatchId, x => x.SystemQuantity);
        var auditDateMap = auditedBatches.ToDictionary(x => x.BatchId, x => x.LatestAuditDate);
        var batchDestroyMap = new Dictionary<int, decimal>();
        foreach (var item in auditedBatches)
        {
            if (!string.IsNullOrEmpty(item.LatestReason))
            {
                var match = System.Text.RegularExpressions.Regex.Match(item.LatestReason, @"\[Tiêu hủy:\s*([\d\.]+)\s*(?:kg|g)?\]");
                if (match.Success && decimal.TryParse(match.Groups[1].Value, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out var qty))
                {
                    batchDestroyMap[item.BatchId] = qty;
                }
            }
        }

        var completedDisposalsList = await _context.DisposalDetails
            .Include(dd => dd.Disposal)
            .Where(dd => dd.Disposal.Status == "Hoàn thành" || dd.Disposal.Status == "Đã tiêu hủy")
            .Select(dd => new { dd.BatchId, dd.DisposalQuantity, dd.Disposal.DisposalDate })
            .ToListAsync(ct);

        var completedDisposals = completedDisposalsList
            .Where(dd => !auditDateMap.TryGetValue(dd.BatchId, out var aDate) || dd.DisposalDate >= aDate.AddMinutes(-5))
            .GroupBy(dd => dd.BatchId)
            .ToDictionary(g => g.Key, g => g.Sum(x => x.DisposalQuantity));

        var pendingDisposals = await _context.DisposalDetails
            .Include(dd => dd.Disposal)
            .Where(dd => dd.Disposal.Status == "Chờ quản lý duyệt" || dd.Disposal.Status == "Đã duyệt / Chờ hủy" || dd.Disposal.Status == "Đã duyệt")
            .GroupBy(dd => dd.BatchId)
            .ToDictionaryAsync(g => g.Key, g => g.Sum(x => x.DisposalQuantity), ct);

        return herbs.Select(h =>
        {
            var activeBatches = h.Batches
                .OrderBy(b => b.ExpiryDate)
                .ThenBy(b => b.ImportDate)
                .Select(b =>
                {
                    var rawDestrQty = batchDestroyMap.TryGetValue(b.BatchId, out var dQty) ? dQty : 0m;
                    var compQty = completedDisposals.TryGetValue(b.BatchId, out var cQty) ? cQty : 0m;
                    var auditPendingDestr = Math.Max(0m, rawDestrQty - compQty);
                    var directPendingDestr = pendingDisposals.TryGetValue(b.BatchId, out var pQty) ? pQty : 0m;
                    var destrQty = Math.Max(auditPendingDestr, directPendingDestr);
                    var usableQty = Math.Max(0m, b.CurrentQuantity - destrQty);
                    return new BatchResponseDto
                    {
                        BatchId = b.BatchId,
                        BatchNumber = b.BatchNumber,
                        WarehouseId = b.WarehouseId,
                        WarehouseName = b.Warehouse?.WarehouseName ?? "Kho Dược liệu trung tâm",
                        ImportDate = b.ImportDate,
                        ExpiryDate = b.ExpiryDate,
                        OriginalQuantity = b.OriginalQuantity,
                        CurrentQuantity = b.CurrentQuantity,
                        SystemQuantity = batchSysMap.TryGetValue(b.BatchId, out var sysQty) ? sysQty : b.CurrentQuantity,
                        UsableQuantity = usableQty,
                        DestroyQuantity = destrQty,
                        UnitCost = b.UnitCost,
                        Status = b.Status
                    };
                })
                .ToList();

            // Tồn kho tổng: giữ nguyên theo hệ thống / sổ sách ban đầu
            var totalSystemStock = activeBatches.Sum(b => b.SystemQuantity);

            // Tồn kho thực tế: được cập nhật theo số thực đếm sau khi kiểm kê
            var actualStock = activeBatches.Sum(b => b.CurrentQuantity);

            // Tiêu hủy và có thể dùng
            var totalDestroyStock = activeBatches.Sum(b => b.DestroyQuantity);
            var totalUsableStock = Math.Max(0m, actualStock - totalDestroyStock);

            var earliestBatch = activeBatches.FirstOrDefault(b => b.CurrentQuantity > 0);
            var nearestExpiryStr = earliestBatch != null
                ? earliestBatch.ExpiryDate.ToString("dd/MM/yyyy")
                : "—";

            var defaultLocation = activeBatches.FirstOrDefault()?.WarehouseName 
                ?? "Kho Dược liệu trung tâm";

            return new HerbInventoryItemDto
            {
                HerbId = h.HerbId,
                HerbCode = h.HerbCode,
                CommonName = h.CommonName,
                ScientificName = h.ScientificName,
                Unit = h.Unit,
                TasteProperties = h.TasteProperties,
                StandardPrice = h.StandardPrice,
                MinThreshold = h.MinThreshold,
                IsActive = h.IsActive,
                TotalStock = totalSystemStock,
                ActualStock = actualStock,
                UsableStock = totalUsableStock,
                DestroyStock = totalDestroyStock,
                NearestExpiry = nearestExpiryStr,
                Location = defaultLocation,
                Batches = activeBatches
            };
        }).ToList();
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

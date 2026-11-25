using System.Data;
using COSGeniusSystem.Data;
using COSGeniusSystem.Models;
using Microsoft.EntityFrameworkCore;

namespace COSGeniusSystem.Services.Herbalist;

public enum HerbalistStorageOutcome { Success, Created, Invalid, NotFound, Conflict }
public record HerbalistStorageResult(HerbalistStorageOutcome Outcome, string Message, object? Data = null);

public interface IHerbalistStorageService
{
    Task<HerbalistStorageResult> ListAsync(int warehouseId, CancellationToken ct);
    Task<HerbalistStorageResult> CreateAsync(int warehouseId, string code, CancellationToken ct);
    Task<HerbalistStorageResult> AssignAsync(int batchId, int storageLocationId, CancellationToken ct);
}

public sealed class HerbalistStorageService(ApplicationDbContext db) : IHerbalistStorageService
{
    public async Task<HerbalistStorageResult> ListAsync(int warehouseId, CancellationToken ct) =>
        new(HerbalistStorageOutcome.Success, "Thành công.", await db.WarehouseStorageLocations.AsNoTracking().Where(x => x.WarehouseId == warehouseId).OrderBy(x => x.Code).ToListAsync(ct));

    public async Task<HerbalistStorageResult> CreateAsync(int warehouseId, string code, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(code) || !await db.Warehouses.AnyAsync(x => x.WarehouseId == warehouseId && x.IsActive, ct))
            return new(HerbalistStorageOutcome.Invalid, "Invalid warehouse or shelf/bin code.");
        var location = new WarehouseStorageLocation { WarehouseId = warehouseId, Code = code.Trim() };
        db.WarehouseStorageLocations.Add(location);
        try { await db.SaveChangesAsync(ct); }
        catch (DbUpdateException) { return new(HerbalistStorageOutcome.Conflict, "The location code already exists in this warehouse."); }
        return new(HerbalistStorageOutcome.Created, "Đã tạo vị trí.", new { location.Id, location.Code, location.WarehouseId });
    }

    public async Task<HerbalistStorageResult> AssignAsync(int batchId, int storageLocationId, CancellationToken ct)
    {
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        var batch = await db.Batches.SingleOrDefaultAsync(x => x.BatchId == batchId, ct);
        if (batch == null) return new(HerbalistStorageOutcome.NotFound, "Batch was not found.");
        if (!await db.WarehouseStorageLocations.AnyAsync(x => x.Id == storageLocationId && x.WarehouseId == batch.WarehouseId && x.IsActive, ct))
            return new(HerbalistStorageOutcome.Invalid, "The location must be active and belong to the same warehouse.");
        batch.StorageLocationId = storageLocationId;
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        return new(HerbalistStorageOutcome.Success, "Đã gán một vị trí cho lô.", new { batch.BatchId, batch.StorageLocationId });
    }
}

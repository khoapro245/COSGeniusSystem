using COSGeniusSystem.Data;
using COSGeniusSystem.DTOs;
using Microsoft.EntityFrameworkCore;

namespace COSGeniusSystem.Services;

public class WarehouseService(ApplicationDbContext context) : IWarehouseService
{
    public Task<List<WarehouseResponseDto>> GetWarehousesAsync(CancellationToken ct = default) =>
        context.Warehouses.AsNoTracking()
            .Where(w => w.IsActive)
            .OrderBy(w => w.WarehouseId)
            .Select(w => new WarehouseResponseDto
            {
                WarehouseId = w.WarehouseId,
                WarehouseCode = w.WarehouseCode,
                WarehouseName = w.WarehouseName,
                Location = w.Location,
                IsActive = w.IsActive
            }).ToListAsync(ct);
}

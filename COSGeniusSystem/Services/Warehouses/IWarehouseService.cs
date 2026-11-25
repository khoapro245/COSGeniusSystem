using COSGeniusSystem.DTOs;

namespace COSGeniusSystem.Services;

public interface IWarehouseService
{
    Task<List<WarehouseResponseDto>> GetWarehousesAsync(CancellationToken ct = default);
}

using COSGeniusSystem.DTOs;

namespace COSGeniusSystem.Services;

public interface IPurchaseOrderService
{
    Task<List<PurchaseOrderDto>> GetPurchaseOrdersAsync(string? status = null, string? search = null, CancellationToken ct = default);
    Task<PurchaseOrderDto?> GetPurchaseOrderDetailAsync(string idOrCode, CancellationToken ct = default);
    Task<PurchaseOrderDto> CreatePOAsync(int userId, CreatePORequest request, CancellationToken ct = default);
    Task<PurchaseOrderDto> UpdatePOAsync(int id, UpdatePORequest request, CancellationToken ct = default);
    Task DeletePOAsync(int id, CancellationToken ct = default);
}


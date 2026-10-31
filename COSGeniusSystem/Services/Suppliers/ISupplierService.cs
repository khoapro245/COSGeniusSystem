using COSGeniusSystem.DTOs.Suppliers;

namespace COSGeniusSystem.Services.Suppliers;

public interface ISupplierService
{
    Task<List<SupplierResponseDto>> GetSuppliersAsync(SupplierFilterQuery query, CancellationToken ct = default);
    Task<SupplierDetailDto?> GetSupplierByIdAsync(int id, CancellationToken ct = default);
    Task<SupplierDetailDto?> GetSupplierByCodeAsync(string code, CancellationToken ct = default);
    Task<(SupplierResponseDto? Dto, string? Error)> CreateSupplierAsync(CreateSupplierRequest request, CancellationToken ct = default);
    Task<(SupplierResponseDto? Dto, string? Error)> UpdateSupplierAsync(int id, UpdateSupplierRequest request, CancellationToken ct = default);
    Task<(bool Success, bool IsDeactivated, string? Error)> DeleteSupplierAsync(int id, CancellationToken ct = default);
}


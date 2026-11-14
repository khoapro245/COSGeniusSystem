using COSGeniusSystem.DTOs;

namespace COSGeniusSystem.Services;

public interface IAuditService
{
    Task<List<InventoryAuditDto>> GetAuditsAsync(CancellationToken ct = default);
    Task<InventoryAuditDto?> GetAuditByIdOrCodeAsync(string idOrCode, CancellationToken ct = default);
    Task<InventoryAuditDto> CreateAuditAsync(CreateAuditRequest request, int currentUserId = 0, CancellationToken ct = default);
    Task<InventoryAuditDto> UpdateAuditStatusAsync(string idOrCode, string newStatus, string? note = null, CancellationToken ct = default);
    Task<InventoryAuditDto> UpdateAuditCountAsync(string idOrCode, UpdateAuditCountRequest request, CancellationToken ct = default);
    Task<InventoryAuditDto> ApproveAuditAdjustmentAsync(string idOrCode, ApproveAuditRequest request, CancellationToken ct = default);
}


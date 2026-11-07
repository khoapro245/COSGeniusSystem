using COSGeniusSystem.DTOs;

namespace COSGeniusSystem.Services;

public interface IGoodsReceiptService
{
    Task<List<GoodsReceiptDto>> GetGoodsReceiptsAsync(string? status = null, int? poId = null, CancellationToken ct = default);
    Task<GoodsReceiptDto?> GetGoodsReceiptDetailAsync(string idOrCode, CancellationToken ct = default);
    Task<GoodsReceiptDto> CreateGoodsReceiptAsync(int userId, CreateGoodsReceiptRequest request, CancellationToken ct = default);
    Task<GoodsReceiptDto> SubmitForApprovalAsync(int receiptId, CancellationToken ct = default);
    Task<GoodsReceiptDto> ApproveGoodsReceiptAsync(int managerId, int receiptId, ApproveGoodsReceiptRequest request, CancellationToken ct = default);
}


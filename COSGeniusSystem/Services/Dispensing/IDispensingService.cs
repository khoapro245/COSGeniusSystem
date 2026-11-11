using COSGeniusSystem.DTOs;
using System.Security.Claims;

namespace COSGeniusSystem.Services;

public interface IDispensingService
{
    // HB-01: Danh sách đơn thuốc chờ bốc
    Task<List<PendingPrescriptionDto>> GetPendingPrescriptionsAsync(string? keyword, CancellationToken ct = default);

    // HB-03 / HB-04: Xem ma trận gộp & đối soát tồn kho
    Task<AggregateSlipPreviewDto> PreviewAggregateSlipAsync(List<int> orderIds, CancellationToken ct = default);

    // HB-03 / HB-04: Tạo phiếu lĩnh gửi sang kho
    Task<DispensingSlipItemDto> CreateDispensingSlipAsync(CreateDispensingSlipRequest request, ClaimsPrincipal user, CancellationToken ct = default);

    // HB-05: Danh sách phiếu lĩnh
    Task<List<DispensingSlipItemDto>> GetDispensingSlipsAsync(DispensingSlipFilterQuery query, CancellationToken ct = default);

    // HB-06, HB-07: Chi tiết phiếu lĩnh
    Task<DispensingSlipDetailDto> GetDispensingSlipDetailAsync(int slipId, CancellationToken ct = default);

    // HB-06: Xác nhận nhận đủ từ kho
    Task<bool> ConfirmReceivedAsync(int slipId, ClaimsPrincipal user, CancellationToken ct = default);

    // HB-07: Bốc xong đơn thuốc
    Task<bool> CompleteDispenseOrderAsync(int orderId, ClaimsPrincipal user, CancellationToken ct = default);

    // HB-09: Lịch sử bốc thuốc
    Task<List<DispensedHistoryRecordDto>> GetDispenseHistoryAsync(DispenseHistoryFilterQuery query, CancellationToken ct = default);
}

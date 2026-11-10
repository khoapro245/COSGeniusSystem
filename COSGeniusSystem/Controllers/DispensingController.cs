using COSGeniusSystem.Commons;
using COSGeniusSystem.DTOs;
using COSGeniusSystem.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace COSGeniusSystem.Controllers;

[ApiController]
[Route("api/v1/dispensing-slips")]
[Authorize]
public class DispensingController : BaseApiController
{
    private readonly IDispensingService _dispensingService;

    public DispensingController(IDispensingService dispensingService)
    {
        _dispensingService = dispensingService;
    }

    /// <summary>
    /// [HB-01] Danh sách đơn thuốc chờ bốc
    /// </summary>
    [HttpGet("pending-orders")]
    public async Task<ActionResult<ApiResponseModel<List<PendingPrescriptionDto>>>> GetPendingOrders(
        [FromQuery] string? keyword, CancellationToken ct = default)
    {
        var result = await _dispensingService.GetPendingPrescriptionsAsync(keyword, ct);
        return SuccessResponse(new SystemMessage(200, "Lấy danh sách đơn thuốc chờ bốc thành công.", true), result);
    }

    /// <summary>
    /// [HB-03 / HB-04] Ma trận cộng gộp nguyên liệu & đối soát tồn kho
    /// </summary>
    [HttpPost("preview-aggregate")]
    public async Task<ActionResult<ApiResponseModel<AggregateSlipPreviewDto>>> PreviewAggregate(
        [FromBody] List<int> orderIds, CancellationToken ct = default)
    {
        var result = await _dispensingService.PreviewAggregateSlipAsync(orderIds, ct);
        return SuccessResponse(new SystemMessage(200, "Xem trước ma trận cộng gộp thành công.", true), result);
    }

    /// <summary>
    /// [HB-03 / HB-04] Tạo phiếu lĩnh gộp gửi sang kho (Hỗ trợ cả POST /api/v1/dispensing-slips và /create-slip)
    /// </summary>
    [HttpPost]
    [HttpPost("create-slip")]
    public async Task<ActionResult<ApiResponseModel<DispensingSlipItemDto>>> CreateSlip(
        [FromBody] CreateDispensingSlipRequest request, CancellationToken ct = default)
    {
        var result = await _dispensingService.CreateDispensingSlipAsync(request, User, ct);
        return SuccessResponse(new SystemMessage(200, "Tạo phiếu lĩnh thuốc và gửi sang kho thành công.", true), result);
    }

    /// <summary>
    /// [HB-05] Danh sách phiếu lĩnh thuốc
    /// </summary>
    [HttpGet]
    public async Task<ActionResult<ApiResponseModel<List<DispensingSlipItemDto>>>> GetSlips(
        [FromQuery] DispensingSlipFilterQuery query, CancellationToken ct = default)
    {
        var result = await _dispensingService.GetDispensingSlipsAsync(query, ct);
        return SuccessResponse(new SystemMessage(200, "Lấy danh sách phiếu lĩnh thành công.", true), result);
    }

    /// <summary>
    /// [HB-06, HB-07] Chi tiết phiếu lĩnh thuốc & danh mục đối chiếu
    /// </summary>
    [HttpGet("{id:int}")]
    public async Task<ActionResult<ApiResponseModel<DispensingSlipDetailDto>>> GetSlipDetail(
        int id, CancellationToken ct = default)
    {
        var result = await _dispensingService.GetDispensingSlipDetailAsync(id, ct);
        return SuccessResponse(new SystemMessage(200, "Lấy chi tiết phiếu lĩnh thành công.", true), result);
    }

    /// <summary>
    /// [HB-06] Xác nhận đã nhận đủ thuốc từ kho (Hỗ trợ cả PUT và POST)
    /// </summary>
    [HttpPost("{id:int}/confirm-received")]
    [HttpPut("{id:int}/confirm-received")]
    public async Task<ActionResult<ApiResponseModel<bool>>> ConfirmReceived(
        int id, CancellationToken ct = default)
    {
        var result = await _dispensingService.ConfirmReceivedAsync(id, User, ct);
        return SuccessResponse(new SystemMessage(200, "Xác nhận nhận đủ thuốc thành công.", true), result);
    }

    /// <summary>
    /// [HB-07] Xác nhận đã bốc xong từng đơn thuốc (Hỗ trợ cả PUT/POST: orders/{orderId}/complete và {id}/orders/{orderId}/dispense)
    /// </summary>
    [HttpPost("orders/{orderId:int}/complete")]
    [HttpPut("orders/{orderId:int}/complete")]
    [HttpPost("{id:int}/orders/{orderId:int}/dispense")]
    [HttpPut("{id:int}/orders/{orderId:int}/dispense")]
    public async Task<ActionResult<ApiResponseModel<bool>>> CompleteDispenseOrder(
        int orderId, [FromRoute] int? id = null, CancellationToken ct = default)
    {
        var result = await _dispensingService.CompleteDispenseOrderAsync(orderId, User, ct);
        return SuccessResponse(new SystemMessage(200, "Xác nhận bốc đơn thuốc thành công.", true), result);
    }

    /// <summary>
    /// [HB-09] Lịch sử bốc thuốc
    /// </summary>
    [HttpGet("history")]
    public async Task<ActionResult<ApiResponseModel<List<DispensedHistoryRecordDto>>>> GetHistory(
        [FromQuery] DispenseHistoryFilterQuery query, CancellationToken ct = default)
    {
        var result = await _dispensingService.GetDispenseHistoryAsync(query, ct);
        return SuccessResponse(new SystemMessage(200, "Lấy lịch sử bốc thuốc thành công.", true), result);
    }
}

using System;
using System.Collections.Generic;

namespace COSGeniusSystem.DTOs;

public class SupplierReturnDetailDto
{
    public int ReturnDetailId { get; set; }
    public int ReturnId { get; set; }
    public int? BatchId { get; set; }
    public int? HerbId { get; set; }
    public string? HerbCode { get; set; }
    public string HerbName { get; set; } = null!;
    public string? SupplierBatchCode { get; set; }
    public string? ExpiryDate { get; set; }
    public decimal AvailableQuantity { get; set; }
    public decimal ReturnQuantity { get; set; }
    public decimal UnitPrice { get; set; }
    public decimal LineTotal { get; set; }
    public string? Reason { get; set; }
    public string? OriginalReceiptCode { get; set; }
}

public class SupplierReturnDto
{
    public int ReturnId { get; set; }
    public string ReturnCode { get; set; } = null!;
    public string SupplierName { get; set; } = null!;
    public int? ReceiptId { get; set; }
    public string ReceiptCode { get; set; } = null!;
    public string BatchCode { get; set; } = null!;
    public int WarehouseId { get; set; }
    public int CreatedBy { get; set; }
    public string? CreatedByName { get; set; }
    public string CreatedDate { get; set; } = null!;
    public decimal TotalReturnQuantity { get; set; }
    public decimal TotalReturnValue { get; set; }
    public string ReasonCategory { get; set; } = null!;
    public string? ReasonNote { get; set; }
    public string Status { get; set; } = null!;
    public int? ApprovedBy { get; set; }
    public string? ApprovedByName { get; set; }
    public string? ApprovedDate { get; set; }
    public string? ApprovalNote { get; set; }
    public int? HandedOverBy { get; set; }
    public string? HandedOverByName { get; set; }
    public string? HandedOverDate { get; set; }
    public string? HandoverNote { get; set; }
    public string? VehicleNumber { get; set; }
    public string? ReceiverName { get; set; }
    public string? RejectionReason { get; set; }
    public List<SupplierReturnDetailDto> Details { get; set; } = new();
}

public class CreateSupplierReturnDetailItem
{
    public int? BatchId { get; set; }
    public int? HerbId { get; set; }
    public string? HerbCode { get; set; }
    public string HerbName { get; set; } = null!;
    public string? SupplierBatchCode { get; set; }
    public string? ExpiryDate { get; set; }
    public decimal AvailableQuantity { get; set; }
    public decimal ReturnQuantity { get; set; }
    public decimal UnitPrice { get; set; }
    public string? Reason { get; set; }
    public string? OriginalReceiptCode { get; set; }
}

public class CreateSupplierReturnRequest
{
    public string? ReturnCode { get; set; }
    public string SupplierName { get; set; } = null!;
    public int? ReceiptId { get; set; }
    public string ReceiptCode { get; set; } = null!;
    public string BatchCode { get; set; } = null!;
    public int WarehouseId { get; set; } = 1;
    public string? CreatedByName { get; set; }
    public string? VehicleNumber { get; set; }
    public string? ReceiverName { get; set; }
    public string ReasonCategory { get; set; } = null!; // Bắt buộc 1 trong 3: 'Nhà sản xuất / NCC thu hồi (Recall)', 'Đổi trả hàng cận date theo hợp đồng nguyên tắc', 'Khác'
    public string? ReasonNote { get; set; }
    public List<CreateSupplierReturnDetailItem> Items { get; set; } = new();
}

public class ApproveSupplierReturnRequest
{
    public string? ApproverName { get; set; }
    public string? ApprovalNote { get; set; }
    public string? VehicleNumber { get; set; }
    public string? ReceiverName { get; set; }
}

public class RejectSupplierReturnRequest
{
    public string Reason { get; set; } = null!;
}

public class HandoverSupplierReturnRequest
{
    public string? HandedOverByName { get; set; }
    public string? HandoverNote { get; set; }
}


using System;
using System.Collections.Generic;

namespace COSGeniusSystem.Models;

public partial class SupplierReturn
{
    public int ReturnId { get; set; }

    public string ReturnCode { get; set; } = null!;

    public string SupplierName { get; set; } = null!;

    public int? ReceiptId { get; set; }

    public string ReceiptCode { get; set; } = null!;

    public string BatchCode { get; set; } = null!;

    public int WarehouseId { get; set; } = 1;

    public int CreatedBy { get; set; }

    public string? CreatedByName { get; set; }

    public DateTime CreatedDate { get; set; } = DateTime.Now;

    public decimal TotalReturnQuantity { get; set; }

    public decimal TotalReturnValue { get; set; }

    public string ReasonCategory { get; set; } = null!;

    public string? ReasonNote { get; set; }

    public string Status { get; set; } = "Đề nghị trả";

    public int? ApprovedBy { get; set; }

    public string? ApprovedByName { get; set; }

    public DateTime? ApprovedDate { get; set; }

    public string? ApprovalNote { get; set; }

    public int? HandedOverBy { get; set; }

    public string? HandedOverByName { get; set; }

    public DateTime? HandedOverDate { get; set; }

    public string? HandoverNote { get; set; }

    public string? VehicleNumber { get; set; }

    public string? ReceiverName { get; set; }

    public string? RejectionReason { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.Now;

    public DateTime UpdatedAt { get; set; } = DateTime.Now;

    public virtual ICollection<SupplierReturnDetail> SupplierReturnDetails { get; set; } = new List<SupplierReturnDetail>();
}


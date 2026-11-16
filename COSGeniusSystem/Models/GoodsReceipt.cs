using System;
using System.Collections.Generic;

namespace COSGeniusSystem.Models;

public partial class GoodsReceipt
{
    public int ReceiptId { get; set; }

    public string ReceiptCode { get; set; } = null!;

    public int? Poid { get; set; }

    public int WarehouseId { get; set; }

    public int ReceivedBy { get; set; }

    public DateTime ReceiptDate { get; set; }

    public decimal TotalCost { get; set; }

    public string? Note { get; set; }

    // [hieppm] Start: Bổ sung thuộc tính Status, DriverName, VehicleNumber, DocumentUrl
    public string Status { get; set; } = "PENDING_APPROVAL";

    public string? DriverName { get; set; }

    public string? VehicleNumber { get; set; }

    public string? DocumentUrl { get; set; }
    // [hieppm] End: Bổ sung thuộc tính Status, DriverName, VehicleNumber, DocumentUrl

    public virtual PurchaseOrder? Po { get; set; }

    public virtual ICollection<ReceiptDetail> ReceiptDetails { get; set; } = new List<ReceiptDetail>();

    public virtual ICollection<ReceiptHistory> ReceiptHistories { get; set; } = new List<ReceiptHistory>();

    public virtual User ReceivedByNavigation { get; set; } = null!;

    public virtual Warehouse Warehouse { get; set; } = null!;
}

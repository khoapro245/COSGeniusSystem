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

    public virtual PurchaseOrder? Po { get; set; }

    public virtual ICollection<ReceiptDetail> ReceiptDetails { get; set; } = new List<ReceiptDetail>();

    public virtual ICollection<ReceiptHistory> ReceiptHistories { get; set; } = new List<ReceiptHistory>();

    public virtual User ReceivedByNavigation { get; set; } = null!;

    public virtual Warehouse Warehouse { get; set; } = null!;
}

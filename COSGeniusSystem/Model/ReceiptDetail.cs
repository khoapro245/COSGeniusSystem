using System;
using System.Collections.Generic;

namespace COSGeniusSystem.Model;

public partial class ReceiptDetail
{
    public int ReceiptDetailId { get; set; }

    public int ReceiptId { get; set; }

    public int BatchId { get; set; }

    public decimal Quantity { get; set; }

    public decimal UnitPrice { get; set; }

    public decimal? LineTotal { get; set; }

    public virtual Batch Batch { get; set; } = null!;

    public virtual GoodsReceipt Receipt { get; set; } = null!;
}

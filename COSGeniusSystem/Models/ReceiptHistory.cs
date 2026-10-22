using System;
using System.Collections.Generic;

namespace COSGeniusSystem.Models;

public partial class ReceiptHistory
{
    public int HistoryId { get; set; }

    public int BatchId { get; set; }

    public int ReceiptId { get; set; }

    public decimal QuantityChanged { get; set; }

    public decimal BalanceAfter { get; set; }

    public DateTime ChangeDate { get; set; }

    public virtual Batch Batch { get; set; } = null!;

    public virtual GoodsReceipt Receipt { get; set; } = null!;
}

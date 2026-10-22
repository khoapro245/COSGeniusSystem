using System;
using System.Collections.Generic;

namespace COSGeniusSystem.Model;

public partial class AuditDetail
{
    public int AuditDetailId { get; set; }

    public int AuditId { get; set; }

    public int BatchId { get; set; }

    public decimal SystemQuantity { get; set; }

    public decimal ActualQuantity { get; set; }

    public decimal? Discrepancy { get; set; }

    public string? Reason { get; set; }

    public virtual InventoryAudit Audit { get; set; } = null!;

    public virtual Batch Batch { get; set; } = null!;
}

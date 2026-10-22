using System;
using System.Collections.Generic;

namespace COSGeniusSystem.Model;

public partial class DisposalDetail
{
    public int DisposalDetailId { get; set; }

    public int DisposalId { get; set; }

    public int BatchId { get; set; }

    public decimal DisposalQuantity { get; set; }

    public string? Notes { get; set; }

    public virtual Batch Batch { get; set; } = null!;

    public virtual Disposal Disposal { get; set; } = null!;
}

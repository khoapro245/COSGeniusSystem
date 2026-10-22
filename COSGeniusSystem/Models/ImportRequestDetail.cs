using System;
using System.Collections.Generic;

namespace COSGeniusSystem.Models;

public partial class ImportRequestDetail
{
    public int RequestDetailId { get; set; }

    public int RequestId { get; set; }

    public int HerbId { get; set; }

    public decimal RequestedQuantity { get; set; }

    public string? Note { get; set; }

    public virtual Herb Herb { get; set; } = null!;

    public virtual ImportRequest Request { get; set; } = null!;
}

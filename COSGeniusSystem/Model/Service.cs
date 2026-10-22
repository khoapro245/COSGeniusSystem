using System;
using System.Collections.Generic;

namespace COSGeniusSystem.Model;

public partial class Service
{
    public int ServiceId { get; set; }

    public string ServiceCode { get; set; } = null!;

    public string ServiceName { get; set; } = null!;

    public decimal Price { get; set; }

    public string Unit { get; set; } = null!;

    public string? Description { get; set; }

    public bool IsActive { get; set; }

    public virtual ICollection<UsageHistory> UsageHistories { get; set; } = new List<UsageHistory>();
}

using System;
using System.Collections.Generic;

namespace COSGeniusSystem.Models;

public partial class TreatmentPlanDetail
{
    public int PlanDetailId { get; set; }

    public int PlanId { get; set; }

    public int ServiceId { get; set; }

    public string ServiceNameSnapshot { get; set; } = null!;

    public string UnitSnapshot { get; set; } = null!;

    public decimal UnitPrice { get; set; }

    public int PlannedQuantity { get; set; }

    public string? Instructions { get; set; }

    public virtual ICollection<InvoiceDetail> InvoiceDetails { get; set; } = new List<InvoiceDetail>();

    public virtual TreatmentPlan Plan { get; set; } = null!;

    public virtual Service Service { get; set; } = null!;

    public virtual ICollection<UsageHistory> UsageHistories { get; set; } = new List<UsageHistory>();
}

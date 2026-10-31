using System;
using System.Collections.Generic;

namespace COSGeniusSystem.Models;

public partial class InvoiceDetail
{
    public int InvoiceDetailId { get; set; }

    public int InvoiceId { get; set; }

    public string ItemType { get; set; } = null!;

    public int? PrescriptionId { get; set; }

    public int? PlanDetailId { get; set; }

    public string ItemName { get; set; } = null!;

    public decimal Quantity { get; set; }

    public decimal UnitPrice { get; set; }

    public decimal? SubTotal { get; set; }

    public virtual Invoice Invoice { get; set; } = null!;

    public virtual TreatmentPlanDetail? PlanDetail { get; set; }

    public virtual HerbalPrescription? Prescription { get; set; }
}

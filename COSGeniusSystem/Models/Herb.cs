using System;
using System.Collections.Generic;

namespace COSGeniusSystem.Models;

public partial class Herb
{
    public int HerbId { get; set; }

    public string HerbCode { get; set; } = null!;

    public string? ScientificName { get; set; }

    public string CommonName { get; set; } = null!;

    public string Unit { get; set; } = null!;

    public string? TasteProperties { get; set; }

    public decimal StandardPrice { get; set; }

    public decimal MinThreshold { get; set; }

    public bool IsActive { get; set; }

    public virtual ICollection<Batch> Batches { get; set; } = new List<Batch>();

    public virtual ICollection<DispenseDetail> DispenseDetails { get; set; } = new List<DispenseDetail>();

    public virtual ICollection<HerbalFormulaDetail> HerbalFormulaDetails { get; set; } = new List<HerbalFormulaDetail>();

    public virtual ICollection<HerbalPrescriptionDetail> HerbalPrescriptionDetails { get; set; } = new List<HerbalPrescriptionDetail>();

    public virtual ICollection<ImportRequestDetail> ImportRequestDetails { get; set; } = new List<ImportRequestDetail>();

    public virtual ICollection<Podetail> Podetails { get; set; } = new List<Podetail>();
}

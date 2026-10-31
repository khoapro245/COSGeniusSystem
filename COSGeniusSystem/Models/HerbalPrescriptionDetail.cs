using System;
using System.Collections.Generic;

namespace COSGeniusSystem.Models;

public partial class HerbalPrescriptionDetail
{
    public int PrescriptionDetailId { get; set; }

    public int PrescriptionId { get; set; }

    public int HerbId { get; set; }

    public string HerbNameSnapshot { get; set; } = null!;

    public decimal DosagePerDose { get; set; }

    public string? RoleInFormula { get; set; }

    public string? Note { get; set; }

    public virtual Herb Herb { get; set; } = null!;

    public virtual HerbalPrescription Prescription { get; set; } = null!;
}

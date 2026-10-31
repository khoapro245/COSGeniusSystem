using System;
using System.Collections.Generic;

namespace COSGeniusSystem.Models;

public partial class HerbalFormula
{
    public int HerbalFormulaId { get; set; }

    public string? FormulaCode { get; set; }

    public string FormulaName { get; set; } = null!;

    public string? UsageInstructions { get; set; }

    public string? IntendedUse { get; set; }

    public string Status { get; set; } = null!;

    public int ProposedByDoctorId { get; set; }

    public DateTime? SubmittedAt { get; set; }

    public int? ReviewedBy { get; set; }

    public DateTime? ReviewedAt { get; set; }

    public string? ReviewNote { get; set; }

    public bool IsActive { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime? UpdatedAt { get; set; }

    public byte[] RowVersion { get; set; } = null!;

    public virtual ICollection<HerbalFormulaDetail> HerbalFormulaDetails { get; set; } = new List<HerbalFormulaDetail>();

    public virtual ICollection<HerbalPrescription> HerbalPrescriptions { get; set; } = new List<HerbalPrescription>();

    public virtual Doctor ProposedByDoctor { get; set; } = null!;

    public virtual User? ReviewedByNavigation { get; set; }
}

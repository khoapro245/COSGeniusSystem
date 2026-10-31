using System;
using System.Collections.Generic;

namespace COSGeniusSystem.Models;

public partial class HerbalFormulaDetail
{
    public int HerbalFormulaDetailId { get; set; }

    public int HerbalFormulaId { get; set; }

    public int HerbId { get; set; }

    public decimal DosagePerDose { get; set; }

    public string? RoleInFormula { get; set; }

    public string? Note { get; set; }

    public virtual Herb Herb { get; set; } = null!;

    public virtual HerbalFormula HerbalFormula { get; set; } = null!;
}

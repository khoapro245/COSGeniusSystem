using System;
using System.Collections.Generic;

namespace COSGeniusSystem.Models;

public partial class FormulaDetail
{
    public int FormulaDetailId { get; set; }

    public int FormulaId { get; set; }

    public int HerbId { get; set; }

    public decimal DosagePerDose { get; set; }

    public string? RoleInFormula { get; set; }

    public string? Note { get; set; }

    public virtual RemedyFormula Formula { get; set; } = null!;

    public virtual Herb Herb { get; set; } = null!;
}

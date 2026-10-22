using System;
using System.Collections.Generic;

namespace COSGeniusSystem.Model;

public partial class RemedyFormula
{
    public int FormulaId { get; set; }

    public int? ExamSheetId { get; set; }

    public string FormulaName { get; set; } = null!;

    public string? UsageInstructions { get; set; }

    public int DosageDays { get; set; }

    public int TotalDoses { get; set; }

    public DateTime CreatedAt { get; set; }

    public virtual ExamSheet? ExamSheet { get; set; }

    public virtual ICollection<FormulaDetail> FormulaDetails { get; set; } = new List<FormulaDetail>();

    public virtual ICollection<MedicineDispense> MedicineDispenses { get; set; } = new List<MedicineDispense>();
}

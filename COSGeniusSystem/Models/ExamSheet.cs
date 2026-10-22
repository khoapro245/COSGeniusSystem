using System;
using System.Collections.Generic;

namespace COSGeniusSystem.Models;

public partial class ExamSheet
{
    public int ExamSheetId { get; set; }

    public int RecordId { get; set; }

    public int DoctorId { get; set; }

    public DateTime ExamDate { get; set; }

    public string? PulseDiagnosis { get; set; }

    public string? TongueDiagnosis { get; set; }

    public string? Symptoms { get; set; }

    public string FinalDiagnosis { get; set; } = null!;

    public string? DoctorAdvice { get; set; }

    public virtual Doctor Doctor { get; set; } = null!;

    public virtual ICollection<Invoice> Invoices { get; set; } = new List<Invoice>();

    public virtual MedicalRecord Record { get; set; } = null!;

    public virtual ICollection<RemedyFormula> RemedyFormulas { get; set; } = new List<RemedyFormula>();

    public virtual ICollection<UsageHistory> UsageHistories { get; set; } = new List<UsageHistory>();
}

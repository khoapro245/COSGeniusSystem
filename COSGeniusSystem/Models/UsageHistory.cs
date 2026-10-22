using System;
using System.Collections.Generic;

namespace COSGeniusSystem.Models;

public partial class UsageHistory
{
    public int UsageId { get; set; }

    public int ExamSheetId { get; set; }

    public int ServiceId { get; set; }

    public int? PerformedBy { get; set; }

    public int Quantity { get; set; }

    public DateTime ExecutionTime { get; set; }

    public string? ResultNote { get; set; }

    public virtual ExamSheet ExamSheet { get; set; } = null!;

    public virtual Doctor? PerformedByNavigation { get; set; }

    public virtual Service Service { get; set; } = null!;
}

using System;
using System.Collections.Generic;

namespace COSGeniusSystem.Models;

public partial class TreatmentSession
{
    public int SessionId { get; set; }

    public int PlanId { get; set; }

    public int SessionNumber { get; set; }

    public int? AssignedDoctorId { get; set; }

    public int? PerformedBy { get; set; }

    public int? RequestedBy { get; set; }

    public DateTime? RequestedAt { get; set; }

    public DateOnly? ScheduledDate { get; set; }

    public DateTime? StartedAt { get; set; }

    public DateTime? CompletedAt { get; set; }

    public string Status { get; set; } = null!;

    public string? ResultNote { get; set; }

    public DateTime CreatedAt { get; set; }

    public byte[] RowVersion { get; set; } = null!;

    public virtual Doctor? AssignedDoctor { get; set; }

    public virtual Doctor? PerformedByNavigation { get; set; }

    public virtual TreatmentPlan Plan { get; set; } = null!;

    public virtual User? RequestedByNavigation { get; set; }

    public virtual ICollection<UsageHistory> UsageHistories { get; set; } = new List<UsageHistory>();
}

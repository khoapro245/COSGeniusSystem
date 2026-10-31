using System;
using System.Collections.Generic;

namespace COSGeniusSystem.Models;

public partial class TreatmentPlan
{
    public int PlanId { get; set; }

    public string? PlanCode { get; set; }

    public int RecordId { get; set; }

    public int DoctorId { get; set; }

    public string PlanName { get; set; } = null!;

    public string TreatmentType { get; set; } = null!;

    public int PlannedSessionCount { get; set; }

    public string? Description { get; set; }

    public string Status { get; set; } = null!;

    public DateOnly? StartDate { get; set; }

    public DateOnly? EndDate { get; set; }

    public DateTime? ConfirmedAt { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime? UpdatedAt { get; set; }

    public byte[] RowVersion { get; set; } = null!;

    public virtual Doctor Doctor { get; set; } = null!;

    public virtual MedicalRecord Record { get; set; } = null!;

    public virtual ICollection<TreatmentPlanDetail> TreatmentPlanDetails { get; set; } = new List<TreatmentPlanDetail>();

    public virtual ICollection<TreatmentSession> TreatmentSessions { get; set; } = new List<TreatmentSession>();
}

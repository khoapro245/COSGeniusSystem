using System;
using System.Collections.Generic;

namespace COSGeniusSystem.Models;

public partial class TreatmentProtocol
{
    public int ProtocolId { get; set; }

    public int RecordId { get; set; }

    public int DoctorId { get; set; }

    public string ProtocolName { get; set; } = null!;

    public DateOnly? StartDate { get; set; }

    public DateOnly? EndDate { get; set; }

    public string? Description { get; set; }

    public int? ExamSheetId { get; set; }

    public string? TreatmentType { get; set; }

    public int? PlannedSessionCount { get; set; }

    public string PlanStatus { get; set; } = "ACTIVE";

    public bool IsLegacy { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime? ConfirmedAt { get; set; }

    public DateTime? UpdatedAt { get; set; }

    public byte[] RowVersion { get; set; } = new byte[8];

    public virtual Doctor Doctor { get; set; } = null!;

    public virtual ExamSheet? ExamSheet { get; set; }

    public virtual MedicalRecord Record { get; set; } = null!;

    public virtual ICollection<TreatmentPlanDetail> TreatmentPlanDetails { get; set; } = new List<TreatmentPlanDetail>();

    public virtual ICollection<TreatmentSession> TreatmentSessions { get; set; } = new List<TreatmentSession>();

}

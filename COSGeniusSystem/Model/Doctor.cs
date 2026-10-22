using System;
using System.Collections.Generic;

namespace COSGeniusSystem.Model;

public partial class Doctor
{
    public int DoctorId { get; set; }

    public int? UserId { get; set; }

    public string FullName { get; set; } = null!;

    public string? Specialization { get; set; }

    public string? LicenseNumber { get; set; }

    public string? Phone { get; set; }

    public bool IsActive { get; set; }

    public virtual ICollection<ExamSheet> ExamSheets { get; set; } = new List<ExamSheet>();

    public virtual ICollection<TreatmentProtocol> TreatmentProtocols { get; set; } = new List<TreatmentProtocol>();

    public virtual ICollection<UsageHistory> UsageHistories { get; set; } = new List<UsageHistory>();

    public virtual User? User { get; set; }
}

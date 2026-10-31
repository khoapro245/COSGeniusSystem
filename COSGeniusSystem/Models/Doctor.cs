using System;
using System.Collections.Generic;

namespace COSGeniusSystem.Models;

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

    public virtual ICollection<ExaminationRegistration> ExaminationRegistrations { get; set; } = new List<ExaminationRegistration>();

    public virtual ICollection<HerbalFormula> HerbalFormulas { get; set; } = new List<HerbalFormula>();

    public virtual ICollection<MedicalRecord> MedicalRecords { get; set; } = new List<MedicalRecord>();

    public virtual ICollection<TreatmentPlan> TreatmentPlans { get; set; } = new List<TreatmentPlan>();

    public virtual ICollection<TreatmentSession> TreatmentSessionAssignedDoctors { get; set; } = new List<TreatmentSession>();

    public virtual ICollection<TreatmentSession> TreatmentSessionPerformedByNavigations { get; set; } = new List<TreatmentSession>();

    public virtual ICollection<UsageHistory> UsageHistories { get; set; } = new List<UsageHistory>();

    public virtual User? User { get; set; }
}

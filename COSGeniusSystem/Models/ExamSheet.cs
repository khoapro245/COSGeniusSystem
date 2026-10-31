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

    public string? FinalDiagnosis { get; set; }

    public string? DoctorAdvice { get; set; }

    public int? RegistrationId { get; set; }

    public string ExaminationStatus { get; set; } = null!;

    public DateTime? StartedAt { get; set; }

    public DateTime? CompletedAt { get; set; }

    public DateTime? UpdatedAt { get; set; }

    public short? PulseRate { get; set; }

    public decimal? Temperature { get; set; }

    public short? RespiratoryRate { get; set; }

    public decimal? HeightCm { get; set; }

    public decimal? WeightKg { get; set; }

    public decimal? SpO2 { get; set; }

    public string? PreliminaryDiagnosis { get; set; }

    public string? Inspection { get; set; }

    public string? ListeningSmelling { get; set; }

    public string? Inquiry { get; set; }

    public string? Palpation { get; set; }

    public string? FourExaminationSummary { get; set; }

    public string? SyndromeDifferentiation { get; set; }

    public string? TraditionalDiagnosis { get; set; }

    public string? Icdcode { get; set; }

    public string? Icddescription { get; set; }

    public string? AllergyNoteSnapshot { get; set; }

    public byte[] RowVersion { get; set; } = null!;

    public virtual Doctor Doctor { get; set; } = null!;

    public virtual ExaminationRegistration? ExaminationRegistration { get; set; }

    public virtual ICollection<HerbalPrescription> HerbalPrescriptions { get; set; } = new List<HerbalPrescription>();

    public virtual ICollection<Invoice> Invoices { get; set; } = new List<Invoice>();

    public virtual MedicalRecord Record { get; set; } = null!;

    public virtual ICollection<RemedyFormula> RemedyFormulas { get; set; } = new List<RemedyFormula>();

    public virtual ICollection<TreatmentProtocol> TreatmentProtocols { get; set; } = new List<TreatmentProtocol>();

    public virtual ICollection<UsageHistory> UsageHistories { get; set; } = new List<UsageHistory>();
}

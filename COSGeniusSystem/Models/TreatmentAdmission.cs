namespace COSGeniusSystem.Models;

// One registration, one patient, one assigned doctor. Never inferred from date/patient.
public sealed class TreatmentAdmission
{
    public Guid AdmissionId { get; set; }
    public Guid RegistrationKey { get; set; }
    public string RequestFingerprint { get; set; } = "";
    public int PatientId { get; set; }
    public int DoctorId { get; set; }
    public DateOnly ScheduledDate { get; set; }
    public string Shift { get; set; } = "";
    public int CreatedBy { get; set; }
    public DateTime CreatedAt { get; set; }
    public int? AcceptedBy { get; set; }
    public DateTime? AcceptedAt { get; set; }
    public int? ClosedBy { get; set; }
    public DateTime? ClosedAt { get; set; }
    public byte[] RowVersion { get; set; } = new byte[8];
    public Patient Patient { get; set; } = null!;
    public Doctor Doctor { get; set; } = null!;
    public ICollection<TreatmentSession> Sessions { get; set; } = new List<TreatmentSession>();
}

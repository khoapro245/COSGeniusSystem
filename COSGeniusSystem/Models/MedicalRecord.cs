namespace COSGeniusSystem.Models;

public partial class MedicalRecord
{
    public int RecordId { get; set; }

    public int PatientId { get; set; }

    public DateTime CreatedDate { get; set; }

    public string? InitialDiagnosis { get; set; }

    public string Status { get; set; } = null!;

    public virtual ICollection<ExamSheet> ExamSheets { get; set; } = new List<ExamSheet>();

    public virtual Patient Patient { get; set; } = null!;

    public virtual ICollection<TreatmentProtocol> TreatmentProtocols { get; set; } = new List<TreatmentProtocol>();
}

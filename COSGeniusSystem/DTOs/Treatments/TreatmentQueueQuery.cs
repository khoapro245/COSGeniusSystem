namespace COSGeniusSystem.DTOs.Treatments;

public sealed class TreatmentQueueQuery
{
    public DateTime? Date { get; set; }
    public int? DoctorId { get; set; }
    public string? Keyword { get; set; }
}


namespace COSGeniusSystem.DTOs.Examinations;

public sealed class ExaminationFilterQuery
{
    public DateTime? FromDate { get; set; }
    public DateTime? ToDate { get; set; }
    public int? DoctorId { get; set; }
    public string? Keyword { get; set; }
    public string? Status { get; set; }
}


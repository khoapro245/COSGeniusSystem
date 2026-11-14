namespace COSGeniusSystem.DTOs.DoctorClinical;

public class DoctorExamFilterDto
{
    public int? DoctorId { get; set; }
    public string? Status { get; set; }
    public string? Phone { get; set; }
    public string? Keyword { get; set; }
    public string? Search { get; set; }
    public DateTime? FromDate { get; set; }
    public DateTime? ToDate { get; set; }
    public string? PatientName { get; set; }
    public string? PatientCode { get; set; }
    public string? VisitCode { get; set; }
    public string? Gender { get; set; }
    public int PageNumber { get; set; } = 1;
    public int PageSize { get; set; } = 10;
}

namespace COSGeniusSystem.DTOs.Examinations;

public sealed class ExaminationVisitDto
{
    public int Stt { get; init; }
    public int ExamSheetId { get; init; }
    public string KcbCode { get; init; } = null!;
    public string? PatientCode { get; init; }
    public string PatientName { get; init; } = null!;
    public DateTime RegisteredDate { get; init; }
    public string DoctorName { get; init; } = null!;
    public bool IsPriority { get; init; }
    public string? Phone { get; init; }
    public string Status { get; init; } = null!;
}


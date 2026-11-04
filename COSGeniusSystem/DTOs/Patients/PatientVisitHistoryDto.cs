namespace COSGeniusSystem.DTOs;

public sealed class PatientVisitHistoryDto
{
    public int RecordId { get; init; }
    public int RegistrationId { get; init; }
    public DateTime ExamDate { get; init; }
    public string KcbCode { get; init; } = null!;
    public string MrCode { get; init; } = null!;
    public string DoctorName { get; init; } = null!;
    public string? Symptoms { get; init; }
    public string Status { get; init; } = "Đã hoàn tất";
}

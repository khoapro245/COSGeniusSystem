namespace COSGeniusSystem.DTOs.DoctorClinical;

public class DoctorExamQueueResponseDto
{
    public int WaitingCount { get; set; }
    public int ExaminingCount { get; set; }
    public int CompletedCount { get; set; }
    public int TotalCount { get; set; }
    public int PageNumber { get; set; }
    public int PageSize { get; set; }
    public List<DoctorExamQueueItemDto> Items { get; set; } = new();
}

public class DoctorExamQueueItemDto
{
    public int ExamSheetId { get; set; }
    public string QueueNumber { get; set; } = string.Empty;
    public bool IsPriority { get; set; }
    public int PatientId { get; set; }
    public string PatientCode { get; set; } = string.Empty;
    public string PatientName { get; set; } = string.Empty;
    public DateTime RegisteredAt { get; set; }
    public string VisitCode { get; set; } = string.Empty;
    public int Age { get; set; }
    public string Gender { get; set; } = string.Empty;
    public string Phone { get; set; } = string.Empty;
    public string Status { get; set; } = "Chờ khám";
    public string AllowedAction { get; set; } = "Gọi vào khám";
}


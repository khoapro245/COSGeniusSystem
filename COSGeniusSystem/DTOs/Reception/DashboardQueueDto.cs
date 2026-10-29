namespace COSGeniusSystem.DTOs.Reception;

public sealed class DashboardQueueDto
{
    public List<DashboardWaitingExamDto> WaitingExaminations { get; init; } = new();
    public List<DashboardWaitingTreatmentDto> WaitingTreatments { get; init; } = new();
}

public sealed class DashboardWaitingExamDto
{
    public int Stt { get; init; }
    public int ExamSheetId { get; init; }
    public string KcbCode { get; init; } = null!;
    public string? PatientCode { get; init; }
    public string FullName { get; init; } = null!;
    public string DoctorName { get; init; } = null!;
    public DateTime ReceptionTime { get; init; }
    public bool IsPriority { get; init; }
}

public sealed class DashboardWaitingTreatmentDto
{
    public int Stt { get; init; }
    public int UsageId { get; init; }
    public string YcdtCode { get; init; } = null!;
    public string? PatientCode { get; init; }
    public string FullName { get; init; } = null!;
    public string ProtocolOrServiceName { get; init; } = null!;
    public string DoctorName { get; init; } = null!;
    public string SessionInfo { get; init; } = null!;
    public DateTime ReceptionTime { get; init; }
}


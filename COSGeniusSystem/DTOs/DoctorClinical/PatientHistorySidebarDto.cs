namespace COSGeniusSystem.DTOs.DoctorClinical;

public class PatientHistorySidebarDto
{
    public List<PreviousVisitBriefDto> PreviousVisits { get; set; } = new();
    public ActiveProtocolBriefDto? ActiveProtocol { get; set; }
}

public class PreviousVisitBriefDto
{
    public int ExamSheetId { get; set; }
    public string MedicalRecordCode { get; set; } = string.Empty;
    public string VisitCode { get; set; } = string.Empty;
    public DateTime ExamDate { get; set; }
    public string DoctorName { get; set; } = string.Empty;
    public string SummarySymptom { get; set; } = string.Empty;
}

public class ActiveProtocolBriefDto
{
    public int ProtocolId { get; set; }
    public string ProtocolCode { get; set; } = string.Empty;
    public string ProtocolName { get; set; } = string.Empty;
    public int CompletedSessions { get; set; }
    public int TotalSessions { get; set; }
    public string PaymentStatus { get; set; } = "Đã thanh toán toàn bộ";
}


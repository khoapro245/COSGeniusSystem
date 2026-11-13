namespace COSGeniusSystem.DTOs.DoctorClinical;

public class MedicalRecordWorkspaceDto
{
    public int ExamSheetId { get; set; }
    public string VisitCode { get; set; } = string.Empty;
    public int MedicalRecordId { get; set; }
    public string MedicalRecordCode { get; set; } = string.Empty;
    public string Status { get; set; } = "Chờ khám";
    public bool IsReadOnly { get; set; }

    public PatientSummaryBannerDto PatientBanner { get; set; } = new();
    public ClinicalExamDataDto ClinicalExam { get; set; } = new();
    public ServiceIndicationDto ServiceIndication { get; set; } = new();
    public PrescriptionDetailDto Prescription { get; set; } = new();
    public PatientHistorySidebarDto PatientHistorySidebar { get; set; } = new();
}


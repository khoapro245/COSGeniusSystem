namespace COSGeniusSystem.DTOs.DoctorClinical;

public class PatientSummaryBannerDto
{
    public int PatientId { get; set; }
    public string PatientCode { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public DateTime? DateOfBirth { get; set; }
    public int Age { get; set; }
    public string Gender { get; set; } = string.Empty;
    public string Phone { get; set; } = string.Empty;
    public string Address { get; set; } = string.Empty;
    public string ContactPersonName { get; set; } = string.Empty;
    public DateTime RegisteredAt { get; set; }
    public string AllergyHistory { get; set; } = string.Empty;
}

public class VitalSignsDto
{
    public int? PulseRate { get; set; }
    public decimal? Temperature { get; set; }
    public int? RespiratoryRate { get; set; }
    public decimal? Height { get; set; }
    public decimal? Weight { get; set; }
    public decimal? SpO2 { get; set; }
}

public class TraditionalFourExamsDto
{
    public string? VongChan { get; set; }
    public string? VanChan_NgheNgui { get; set; }
    public string? VanChan_Hoi { get; set; }
    public string? ThietChan { get; set; }
    public string? FourExamsSummary { get; set; }
    public string? BienChung { get; set; }
    public string? TraditionalDiagnosis { get; set; }
}

public class ClinicalExamDataDto
{
    public DateTime? ExamStartTime { get; set; }
    public int DoctorId { get; set; }
    public string DoctorName { get; set; } = string.Empty;
    public VitalSignsDto VitalSigns { get; set; } = new();
    public string Symptoms { get; set; } = string.Empty;
    public string PreliminaryDiagnosis { get; set; } = string.Empty;
    public TraditionalFourExamsDto TraditionalMedicine { get; set; } = new();
    public string IcdCode { get; set; } = string.Empty;
    public string IcdName { get; set; } = string.Empty;
}

public class SaveClinicalExamRequestDto
{
    public DateTime? ExamStartTime { get; set; }
    public VitalSignsDto? VitalSigns { get; set; }
    public string? Symptoms { get; set; }
    public string? PreliminaryDiagnosis { get; set; }
    public TraditionalFourExamsDto? TraditionalMedicine { get; set; }
    public string? IcdCode { get; set; }
    public string? IcdName { get; set; }
}


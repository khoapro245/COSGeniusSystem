namespace COSGeniusSystem.DTOs;

public class DoctorClinicFilterQuery
{
    public string? Keyword { get; set; }
    public string? Specialization { get; set; }
    public DateOnly? BusinessDate { get; set; }
}

public class DoctorClinicItemDto
{
    public int DoctorId { get; set; }
    public string FullName { get; set; } = null!;
    public string? Specialization { get; set; }
    public string? LicenseNumber { get; set; }
    public string? Phone { get; set; }
    public string Room { get; set; } = null!;
    public bool IsActive { get; set; }
    public string StatusText { get; set; } = null!;
    public string? AbsenceReason { get; set; }
    public int WaitingCount { get; set; }
    public string? CurrentExaminingPatient { get; set; }
    public int WaitingTreatmentCount { get; set; }
    public List<string> CurrentTreatingPatients { get; set; } = new();
}

public class DoctorClinicOverviewDto
{
    public int OnDutyDoctorCount { get; set; }
    public int TotalDoctorCount { get; set; }
    public int WaitingExaminationCount { get; set; }
    public int WaitingTreatmentCount { get; set; }
    public List<string> Specializations { get; set; } = new();
    public List<DoctorClinicItemDto> Doctors { get; set; } = new();
}


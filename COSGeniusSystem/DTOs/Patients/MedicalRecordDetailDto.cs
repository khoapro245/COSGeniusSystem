namespace COSGeniusSystem.DTOs;

public sealed class MedicalRecordDetailDto
{
    // Thông tin khám
    public int RecordId { get; init; }
    public int RegistrationId { get; init; }
    public string KcbCode { get; init; } = null!;
    public DateTime ExamDate { get; init; }
    public string DoctorName { get; init; } = null!;
    public string? Symptoms { get; init; }
    public string? FinalDiagnosis { get; init; } = null!;
    public string? Palpation { get; init; }
    public string? Inspection { get; init; }
    public string Status { get; init; } = null!;
    public string? DoctorAdvice { get; init; }

    // Danh sách dịch vụ chỉ định
    public List<UsageHistoryItemDto> Services { get; init; } = new();

    // Đơn thuốc
    public List<PrescriptionDto> Prescriptions { get; init; } = new();
}

public sealed class UsageHistoryItemDto
{
    public int UsageId { get; init; }
    public int SessionId { get; init; }
    public int PlanDetailId { get; init; }
    public string YcdtCode { get; init; } = null!;
    public string ServiceName { get; init; } = null!;
    public int Quantity { get; init; }
    public DateTime ExecutionTime { get; init; }
    public string? PerformedByDoctor { get; init; }
    public string? ResultNote { get; init; }
}

public sealed class PrescriptionDto
{
    public int PrescriptionId { get; init; }
    public string? PrescriptionName { get; init; } = null!;
    public string? UsageInstructions { get; init; }
    public int? DosageDays { get; init; }
    public string Status { get; init; } = null!;
    public int TotalDoses { get; init; }
    public List<FormulaDetailItemDto> Details { get; init; } = new();
}

public sealed class FormulaDetailItemDto
{
    public int HerbId { get; init; }
    public string HerbName { get; init; } = null!;
    public decimal DosagePerDose { get; init; }
    public string? RoleInFormula { get; init; }
    public string? Note { get; init; }
}

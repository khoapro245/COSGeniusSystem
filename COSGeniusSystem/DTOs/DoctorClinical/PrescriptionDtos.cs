namespace COSGeniusSystem.DTOs.DoctorClinical;

public class PrescriptionDetailDto
{
    public int? FormulaId { get; set; }
    public string PrescriptionCode { get; set; } = string.Empty;
    public string PrescriptionMethod { get; set; } = "Tự kê đơn"; // "Công thức đã duyệt" / "Tự kê đơn"
    public int? TemplateFormulaId { get; set; }
    public string? TemplateFormulaName { get; set; }
    public int TotalDoses { get; set; } = 1;
    public string PrescriptionNote { get; set; } = string.Empty;
    public string Status { get; set; } = "Nháp"; // "Nháp" / "Đã gửi Herbalist"
    public bool IsLocked { get; set; }
    public List<PrescriptionIngredientDto> Ingredients { get; set; } = new();
}

public class PrescriptionIngredientDto
{
    public int HerbId { get; set; }
    public string HerbCode { get; set; } = string.Empty;
    public string HerbName { get; set; } = string.Empty;
    public decimal GramsPerDose { get; set; }
    public int TotalDoses { get; set; }
    public decimal TotalGrams { get; set; }
    public string? Note { get; set; }
}

public class SavePrescriptionRequestDto
{
    public string ActionType { get; set; } = "SAVE_DRAFT"; // "SAVE_DRAFT" hoặc "SEND_HERBALIST"
    public string? PrescriptionMethod { get; set; } = "Tự kê đơn";
    public int? TemplateFormulaId { get; set; }
    public int TotalDoses { get; set; } = 1;
    public string? PrescriptionNote { get; set; }
    public List<SavePrescriptionIngredientDto> Ingredients { get; set; } = new();
}

public class SavePrescriptionIngredientDto
{
    public int HerbId { get; set; }
    public decimal GramsPerDose { get; set; }
    public string? Note { get; set; }
}

public class PrescriptionCatalogDto
{
    public List<FormulaTemplateDto> FormulaTemplates { get; set; } = new();
    public List<HerbItemDto> Herbs { get; set; } = new();
}

public class FormulaTemplateDto
{
    public int FormulaId { get; set; }
    public string FormulaName { get; set; } = string.Empty;
    public string? UsageInstructions { get; set; }
    public int DosageDays { get; set; }
    public int TotalDoses { get; set; }
    public List<FormulaTemplateIngredientDto> Ingredients { get; set; } = new();
}

public class FormulaTemplateIngredientDto
{
    public int HerbId { get; set; }
    public string HerbCode { get; set; } = string.Empty;
    public string HerbName { get; set; } = string.Empty;
    public decimal DosagePerDose { get; set; }
    public string? RoleInFormula { get; set; }
    public string? Note { get; set; }
}

public class HerbItemDto
{
    public int HerbId { get; set; }
    public string HerbCode { get; set; } = string.Empty;
    public string CommonName { get; set; } = string.Empty;
    public string Unit { get; set; } = string.Empty;
    public decimal StandardPrice { get; set; }
    public bool IsActive { get; set; }
}


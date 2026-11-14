namespace COSGeniusSystem.DTOs.Formulas;

public sealed record HerbalFormulaItemDto(
    int HerbalFormulaDetailId,
    int HerbId,
    string HerbCode,
    string HerbName,
    string Unit,
    decimal DosagePerDose,
    decimal? PreviousDosagePerDose,
    string? RoleInFormula,
    string? Note,
    bool IsActive
);

public sealed record HerbalFormulaDto(
    int HerbalFormulaId,
    string? FormulaCode,
    string FormulaName,
    string? Version,
    string? ProposalType,
    string? BaseFormulaCode,
    string? ProposalReason,
    string? UsageInstructions,
    string? IntendedUse,
    string Status,
    int ProposedByDoctorId,
    string? DoctorName,
    DateTime? SubmittedAt,
    int? ReviewedBy,
    string? ReviewerName,
    DateTime? ReviewedAt,
    string? ReviewNote,
    bool IsActive,
    DateTime CreatedAt,
    DateTime? UpdatedAt,
    byte[] RowVersion,
    IReadOnlyList<HerbalFormulaItemDto> Items
);

public sealed record CreateFormulaItemRequest(
    int HerbId,
    decimal DosagePerDose,
    string? RoleInFormula,
    string? Note
);

public sealed class CreateHerbalFormulaRequest
{
    public string? FormulaCode { get; set; }
    public string FormulaName { get; set; } = null!;
    public string? Version { get; set; }
    public string? ProposalType { get; set; } // "CREATE" or "UPDATE"
    public string? BaseFormulaCode { get; set; }
    public string? ProposalReason { get; set; }
    public string? UsageInstructions { get; set; }
    public string? IntendedUse { get; set; }
    public List<CreateFormulaItemRequest> Items { get; set; } = new();
}

public sealed class UpdateHerbalFormulaRequest
{
    public string FormulaName { get; set; } = null!;
    public string? Version { get; set; }
    public string? UsageInstructions { get; set; }
    public string? IntendedUse { get; set; }
    public bool? IsActive { get; set; }
    public List<CreateFormulaItemRequest> Items { get; set; } = new();
}

public sealed class FormulaFilterQuery
{
    public bool? ProposalsOnly { get; set; }
    public string? Keyword { get; set; }
    public string? Status { get; set; } // PENDING, APPROVED, REJECTED, ALL
    public bool? IsActive { get; set; }
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 10;
}

public sealed class ReviewFormulaRequest
{
    public bool Approved { get; set; }
    public string? ReviewNote { get; set; }
}


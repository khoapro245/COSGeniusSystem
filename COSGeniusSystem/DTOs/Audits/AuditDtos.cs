namespace COSGeniusSystem.DTOs;

public class AuditDetailDto
{
    public int AuditDetailId { get; set; }
    public int AuditId { get; set; }
    public int BatchId { get; set; }
    public string BatchNumber { get; set; } = string.Empty;
    public int HerbId { get; set; }
    public string HerbCode { get; set; } = string.Empty;
    public string HerbName { get; set; } = string.Empty;
    public string Unit { get; set; } = "kg";
    public string Location { get; set; } = string.Empty;
    public decimal SystemQuantity { get; set; }
    public decimal ActualQuantity { get; set; }
    public decimal Discrepancy { get; set; }
    public string? Reason { get; set; }
    public string ResolutionStatus { get; set; } = string.Empty;
}

public class InventoryAuditDto
{
    public int AuditId { get; set; }
    public string AuditCode { get; set; } = string.Empty;
    public int WarehouseId { get; set; }
    public string WarehouseName { get; set; } = string.Empty;
    public int AuditedBy { get; set; }
    public string AuditorName { get; set; } = string.Empty;
    public string AuditorRole { get; set; } = string.Empty;
    public DateTime AuditDate { get; set; }
    public string Status { get; set; } = string.Empty;
    public string? Note { get; set; }
    public string Scope { get; set; } = "Toàn kho";
    public int TotalItems { get; set; }
    public int DiscrepancyItems { get; set; }
    public List<AuditDetailDto> Details { get; set; } = new();
}

public class CreateAuditRequest
{
    public int WarehouseId { get; set; } = 1;
    public int? AuditedBy { get; set; }
    public string? AuditorName { get; set; }
    public string Scope { get; set; } = "Toàn kho";
    public DateTime? ScheduledDate { get; set; }
    public string? Note { get; set; }
    public string? InitialStatus { get; set; }
    public List<int>? BatchIds { get; set; }
}

public class UpdateAuditCountItem
{
    public int BatchId { get; set; }
    public decimal ActualQuantity { get; set; }
    public string? Reason { get; set; }
}

public class UpdateAuditCountRequest
{
    public List<UpdateAuditCountItem> Items { get; set; } = new();
    public string? Note { get; set; }
    public bool IsComplete { get; set; }
}

public class ApproveAuditRequest
{
    public string? ManagerOpinion { get; set; }
}


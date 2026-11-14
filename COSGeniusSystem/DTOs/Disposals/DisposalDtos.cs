namespace COSGeniusSystem.DTOs;

public class DisposalDetailDto
{
    public int DisposalDetailId { get; set; }
    public int DisposalId { get; set; }
    public int BatchId { get; set; }
    public string BatchNumber { get; set; } = string.Empty;
    public int HerbId { get; set; }
    public string HerbCode { get; set; } = string.Empty;
    public string HerbName { get; set; } = string.Empty;
    public string Unit { get; set; } = "g";
    public string Location { get; set; } = string.Empty;
    public decimal CurrentStock { get; set; }
    public decimal DisposalQuantity { get; set; }
    public string? ExpiryDate { get; set; }
    public string? Notes { get; set; }
}

public class DisposalDto
{
    public int DisposalId { get; set; }
    public string DisposalCode { get; set; } = string.Empty;
    public int WarehouseId { get; set; }
    public string WarehouseName { get; set; } = string.Empty;
    public int CreatedBy { get; set; }
    public string CreatorName { get; set; } = string.Empty;
    public DateTime DisposalDate { get; set; }
    public string Reason { get; set; } = string.Empty;
    public string Status { get; set; } = "Chờ quản lý duyệt";
    public string? ApproverName { get; set; }
    public string? ApprovedDate { get; set; }
    public string? ApprovalNote { get; set; }
    public string? ExecutorName { get; set; }
    public string? ExecutedDate { get; set; }
    public string? ExecutionNote { get; set; }
    public List<DisposalDetailDto> Details { get; set; } = new();
}

public class CreateDisposalDetailItem
{
    public int BatchId { get; set; }
    public decimal DisposalQuantity { get; set; }
    public string? Notes { get; set; }
}

public class CreateDisposalRequest
{
    public string? DisposalCode { get; set; }
    public int WarehouseId { get; set; } = 1;
    public int? CreatedBy { get; set; }
    public string? CreatorName { get; set; }
    public string Reason { get; set; } = string.Empty;
    public string? Status { get; set; } = "Chờ quản lý duyệt";
    public List<CreateDisposalDetailItem> Items { get; set; } = new();
}

public class ApproveDisposalRequest
{
    public string? ApproverName { get; set; }
    public string? ApprovalNote { get; set; }
}

public class ExecuteDisposalRequest
{
    public string? ExecutorName { get; set; }
    public string? ExecutionNote { get; set; }
}


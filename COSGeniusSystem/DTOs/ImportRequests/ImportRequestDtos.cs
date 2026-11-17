// [hieppm] Start: DTOs cho luồng Đề xuất & Yêu cầu bổ sung kho (ImportRequest)
namespace COSGeniusSystem.DTOs;

public class ImportRequestDetailDto
{
    public int RequestDetailId { get; set; }
    public int RequestId { get; set; }
    public int HerbId { get; set; }
    public string HerbCode { get; set; } = string.Empty;
    public string HerbName { get; set; } = string.Empty;
    public string Unit { get; set; } = "g";
    public decimal RequestedQuantity { get; set; }
    public decimal CurrentStockKg { get; set; }
    public string? Note { get; set; }
}

public class ImportRequestDto
{
    public int RequestId { get; set; }
    public string RequestCode { get; set; } = string.Empty;
    public int RequestedBy { get; set; }
    public string CreatorName { get; set; } = string.Empty;
    public DateTime RequestDate { get; set; }
    public string Status { get; set; } = "Chờ duyệt";
    public string? Reason { get; set; }
    public int MedicineCount => Details.Count;
    public string? LinkedPoCode { get; set; }
    public int? LinkedPoId { get; set; }
    public List<ImportRequestDetailDto> Details { get; set; } = new();
}

public class CreateImportRequestItemRequest
{
    public int HerbId { get; set; }
    public decimal RequestedQuantity { get; set; }
    public string? Note { get; set; }
}

public class CreateImportRequestRequest
{
    public string? Reason { get; set; }
    public List<CreateImportRequestItemRequest> Items { get; set; } = new();
}

public class ReviewImportRequestRequest
{
    public bool IsApproved { get; set; }
    public string? ManagerNote { get; set; }
}
// [hieppm] End: DTOs cho luồng Đề xuất & Yêu cầu bổ sung kho

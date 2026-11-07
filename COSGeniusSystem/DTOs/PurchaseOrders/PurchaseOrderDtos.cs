namespace COSGeniusSystem.DTOs;

public class PODetailDto
{
    public int PodetailId { get; set; }
    public int Poid { get; set; }
    public int HerbId { get; set; }
    public string HerbCode { get; set; } = string.Empty;
    public string HerbName { get; set; } = string.Empty;
    public string Unit { get; set; } = "kg";
    public decimal OrderQuantity { get; set; }
    public decimal ReceivedQuantity { get; set; }
    public decimal RemainingQuantity { get; set; }
    public decimal UnitPrice { get; set; }
    public decimal LineTotal { get; set; }
}

public class PurchaseOrderDto
{
    public int Poid { get; set; }
    public string Pocode { get; set; } = string.Empty;
    public int SupplierId { get; set; }
    public string SupplierName { get; set; } = string.Empty;
    public int CreatedBy { get; set; }
    public string CreatorName { get; set; } = string.Empty;
    public int? RequestId { get; set; }
    public DateTime OrderDate { get; set; }
    public DateOnly? DeliveryDate { get; set; }
    public decimal TotalAmount { get; set; }
    public string Status { get; set; } = string.Empty;
    public string ProgressStatus { get; set; } = "Chờ nhận";
    public string DetailStatus { get; set; } = "Chờ nhận hàng";
    public string? Note { get; set; }
    public List<PODetailDto> Details { get; set; } = new();
}

public class CreatePOItemRequest
{
    public int HerbId { get; set; }
    public decimal OrderQuantity { get; set; }
    public decimal UnitPrice { get; set; }
}

public class CreatePORequest
{
    public int SupplierId { get; set; }
    public DateOnly? DeliveryDate { get; set; }
    public string? Note { get; set; }
    public string Status { get; set; } = "Nháp";
    public List<CreatePOItemRequest> Items { get; set; } = new();
}

public class UpdatePORequest
{
    public int SupplierId { get; set; }
    public DateOnly? DeliveryDate { get; set; }
    public string? Note { get; set; }
    public string? Status { get; set; }
    public List<CreatePOItemRequest> Items { get; set; } = new();
}


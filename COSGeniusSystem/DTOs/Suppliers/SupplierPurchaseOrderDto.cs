namespace COSGeniusSystem.DTOs.Suppliers;

public class SupplierPurchaseOrderDto
{
    public int PoId { get; set; }
    public string PoCode { get; set; } = null!;
    public DateTime OrderDate { get; set; }
    public DateOnly? DeliveryDate { get; set; }
    public decimal TotalAmount { get; set; }
    public string Status { get; set; } = null!;
}


namespace COSGeniusSystem.DTOs.Suppliers;

public class SupplierDetailDto : SupplierResponseDto
{
    public List<SupplierPurchaseOrderDto> PurchaseOrders { get; set; } = new();
}


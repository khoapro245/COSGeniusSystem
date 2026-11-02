namespace COSGeniusSystem.DTOs;

public class SupplierDetailDto : SupplierResponseDto
{
    public List<SupplierPurchaseOrderDto> PurchaseOrders { get; set; } = new();
}


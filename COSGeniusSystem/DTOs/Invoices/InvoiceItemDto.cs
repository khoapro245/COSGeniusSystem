namespace COSGeniusSystem.DTOs;

public class InvoiceItemDto
{
    public string? TreatmentType { get; set; }
    public string? Unit { get; set; }
    public List<InvoiceIngredient> Ingredients { get; set; } = [];
    public int InvoiceDetailId { get; set; }
    public string ItemType { get; set; } = string.Empty;
    public int? PrescriptionId { get; set; }
    public int? PlanDetailId { get; set; }
    public string ItemName { get; set; } = string.Empty;
    public decimal Quantity { get; set; }
    public decimal UnitPrice { get; set; }
    public decimal SubTotal { get; set; }
}

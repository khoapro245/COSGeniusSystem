namespace COSGeniusSystem.DTOs.Invoices;

public class InvoiceItemDto
{
    public int InvoiceDetailId { get; set; }
    public string ItemType { get; set; } = string.Empty;
    public int ReferenceId { get; set; }
    public string ItemName { get; set; } = string.Empty;
    public decimal Quantity { get; set; }
    public decimal UnitPrice { get; set; }
    public decimal SubTotal { get; set; }
}

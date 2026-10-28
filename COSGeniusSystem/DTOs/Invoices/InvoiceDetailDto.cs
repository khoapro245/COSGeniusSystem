namespace COSGeniusSystem.DTOs.Invoices;

public class InvoiceDetailDto
{
    public int InvoiceId { get; set; }
    public string InvoiceCode { get; set; } = string.Empty;
    public DateTime IssuedDate { get; set; }
    public string PaymentMethod { get; set; } = string.Empty;
    public string PaymentStatus { get; set; } = string.Empty;

    // Patient and Doctor information
    public string PatientName { get; set; } = string.Empty;
    public string? PatientCode { get; set; }
    public string DoctorName { get; set; } = string.Empty;
    public string CashierName { get; set; } = string.Empty;

    // List of item details
    public List<InvoiceItemDto> Items { get; set; } = new();

    // Summary totals
    public decimal TotalAmount { get; set; }
    public decimal Discount { get; set; }
    public decimal FinalAmount { get; set; }
}

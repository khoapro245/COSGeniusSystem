namespace COSGeniusSystem.DTOs.Invoices;

public class InvoiceQueryParams
{
    public DateTime? FromDate { get; set; }
    public DateTime? ToDate { get; set; }
    public string? PaymentStatus { get; set; }
    public string? Keyword { get; set; }
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 20;
}

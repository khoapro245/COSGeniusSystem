namespace COSGeniusSystem.Models;

public partial class Invoice
{
    public int InvoiceId { get; set; }

    public string InvoiceCode { get; set; } = null!;

    public int ExamSheetId { get; set; }

    public int CashierId { get; set; }

    public decimal TotalAmount { get; set; }

    public decimal Discount { get; set; }

    public decimal FinalAmount { get; set; }

    public string PaymentMethod { get; set; } = null!;

    public string PaymentStatus { get; set; } = null!;

    public DateTime IssuedDate { get; set; }

    public virtual User Cashier { get; set; } = null!;

    public virtual ExamSheet ExamSheet { get; set; } = null!;

    public virtual ICollection<InvoiceDetail> InvoiceDetails { get; set; } = new List<InvoiceDetail>();
}

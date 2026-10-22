namespace COSGeniusSystem.Models;

public partial class InvoiceDetail
{
    public int InvoiceDetailId { get; set; }

    public int InvoiceId { get; set; }

    public string ItemType { get; set; } = null!;

    public int ReferenceId { get; set; }

    public string ItemName { get; set; } = null!;

    public decimal Quantity { get; set; }

    public decimal UnitPrice { get; set; }

    public decimal? SubTotal { get; set; }

    public virtual Invoice Invoice { get; set; } = null!;
}

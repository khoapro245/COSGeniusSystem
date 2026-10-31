using System;
using System.Collections.Generic;

namespace COSGeniusSystem.Models;

public partial class PurchaseOrder
{
    public int Poid { get; set; }

    public string Pocode { get; set; } = null!;

    public int SupplierId { get; set; }

    public int CreatedBy { get; set; }

    public int? RequestId { get; set; }

    public DateTime OrderDate { get; set; }

    public DateOnly? DeliveryDate { get; set; }

    public decimal TotalAmount { get; set; }

    public string Status { get; set; } = null!;

    public virtual User CreatedByNavigation { get; set; } = null!;

    public virtual ICollection<GoodsReceipt> GoodsReceipts { get; set; } = new List<GoodsReceipt>();

    public virtual ICollection<Podetail> Podetails { get; set; } = new List<Podetail>();

    public virtual ImportRequest? Request { get; set; }

    public virtual Supplier Supplier { get; set; } = null!;
}

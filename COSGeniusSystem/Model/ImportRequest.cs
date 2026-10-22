using System;
using System.Collections.Generic;

namespace COSGeniusSystem.Model;

public partial class ImportRequest
{
    public int RequestId { get; set; }

    public string RequestCode { get; set; } = null!;

    public int RequestedBy { get; set; }

    public DateTime RequestDate { get; set; }

    public string Status { get; set; } = null!;

    public string? Reason { get; set; }

    public virtual ICollection<ImportRequestDetail> ImportRequestDetails { get; set; } = new List<ImportRequestDetail>();

    public virtual ICollection<PurchaseOrder> PurchaseOrders { get; set; } = new List<PurchaseOrder>();

    public virtual User RequestedByNavigation { get; set; } = null!;
}

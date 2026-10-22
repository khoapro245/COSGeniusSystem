using System;
using System.Collections.Generic;

namespace COSGeniusSystem.Model;

public partial class Disposal
{
    public int DisposalId { get; set; }

    public string DisposalCode { get; set; } = null!;

    public int WarehouseId { get; set; }

    public int CreatedBy { get; set; }

    public DateTime DisposalDate { get; set; }

    public string Reason { get; set; } = null!;

    public string Status { get; set; } = null!;

    public virtual User CreatedByNavigation { get; set; } = null!;

    public virtual ICollection<DisposalDetail> DisposalDetails { get; set; } = new List<DisposalDetail>();

    public virtual Warehouse Warehouse { get; set; } = null!;
}

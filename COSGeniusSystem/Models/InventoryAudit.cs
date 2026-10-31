using System;
using System.Collections.Generic;

namespace COSGeniusSystem.Models;

public partial class InventoryAudit
{
    public int AuditId { get; set; }

    public string AuditCode { get; set; } = null!;

    public int WarehouseId { get; set; }

    public int AuditedBy { get; set; }

    public DateTime AuditDate { get; set; }

    public string Status { get; set; } = null!;

    public string? Note { get; set; }

    public virtual ICollection<AuditDetail> AuditDetails { get; set; } = new List<AuditDetail>();

    public virtual User AuditedByNavigation { get; set; } = null!;

    public virtual Warehouse Warehouse { get; set; } = null!;
}

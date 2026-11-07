using System;
using System.Collections.Generic;

namespace COSGeniusSystem.Models;

public partial class Batch
{
    public int BatchId { get; set; }

    public string BatchNumber { get; set; } = null!;

    public int HerbId { get; set; }

    public int WarehouseId { get; set; }

    public DateTime ImportDate { get; set; }

    public DateOnly ExpiryDate { get; set; }

    public decimal OriginalQuantity { get; set; }

    public decimal CurrentQuantity { get; set; }

    public decimal UnitCost { get; set; }

    public string Status { get; set; } = null!;

    // [hieppm] Start: Bổ sung thuộc tính Location và SupplierBatchCode theo Warehouse_Update.sql
    public string? Location { get; set; }

    public string? SupplierBatchCode { get; set; }
    // [hieppm] End: Bổ sung thuộc tính Location và SupplierBatchCode theo Warehouse_Update.sql

    public virtual ICollection<AuditDetail> AuditDetails { get; set; } = new List<AuditDetail>();

    public virtual ICollection<DispenseDetail> DispenseDetails { get; set; } = new List<DispenseDetail>();

    public virtual ICollection<DisposalDetail> DisposalDetails { get; set; } = new List<DisposalDetail>();

    public virtual Herb Herb { get; set; } = null!;

    public virtual ICollection<ReceiptDetail> ReceiptDetails { get; set; } = new List<ReceiptDetail>();

    public virtual ICollection<ReceiptHistory> ReceiptHistories { get; set; } = new List<ReceiptHistory>();

    public virtual Warehouse Warehouse { get; set; } = null!;
}

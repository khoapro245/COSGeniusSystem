using System;
using System.Collections.Generic;

namespace COSGeniusSystem.Models;

public partial class Warehouse
{
    public int WarehouseId { get; set; }

    public string WarehouseCode { get; set; } = null!;

    public string WarehouseName { get; set; } = null!;

    public string? Location { get; set; }

    public bool IsActive { get; set; }

    public virtual ICollection<Batch> Batches { get; set; } = new List<Batch>();

    public virtual ICollection<Disposal> Disposals { get; set; } = new List<Disposal>();

    public virtual ICollection<GoodsReceipt> GoodsReceipts { get; set; } = new List<GoodsReceipt>();

    public virtual ICollection<InventoryAudit> InventoryAudits { get; set; } = new List<InventoryAudit>();

    public virtual ICollection<MedicineDispense> MedicineDispenses { get; set; } = new List<MedicineDispense>();
}

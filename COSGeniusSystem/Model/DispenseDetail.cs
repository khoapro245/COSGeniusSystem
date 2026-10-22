using System;
using System.Collections.Generic;

namespace COSGeniusSystem.Model;

public partial class DispenseDetail
{
    public int DispenseDetailId { get; set; }

    public int DispenseId { get; set; }

    public int BatchId { get; set; }

    public int HerbId { get; set; }

    public decimal QuantityTaken { get; set; }

    public virtual Batch Batch { get; set; } = null!;

    public virtual MedicineDispense Dispense { get; set; } = null!;

    public virtual Herb Herb { get; set; } = null!;
}

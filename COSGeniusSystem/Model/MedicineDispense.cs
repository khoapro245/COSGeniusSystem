using System;
using System.Collections.Generic;

namespace COSGeniusSystem.Model;

public partial class MedicineDispense
{
    public int DispenseId { get; set; }

    public string DispenseCode { get; set; } = null!;

    public int FormulaId { get; set; }

    public int WarehouseId { get; set; }

    public int DispensedBy { get; set; }

    public DateTime DispenseDate { get; set; }

    public string Status { get; set; } = null!;

    public string? Note { get; set; }

    public virtual ICollection<DispenseDetail> DispenseDetails { get; set; } = new List<DispenseDetail>();

    public virtual User DispensedByNavigation { get; set; } = null!;

    public virtual RemedyFormula Formula { get; set; } = null!;

    public virtual Warehouse Warehouse { get; set; } = null!;
}

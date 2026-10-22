using System;
using System.Collections.Generic;

namespace COSGeniusSystem.Model;

public partial class Podetail
{
    public int PodetailId { get; set; }

    public int Poid { get; set; }

    public int HerbId { get; set; }

    public decimal OrderQuantity { get; set; }

    public decimal UnitPrice { get; set; }

    public decimal? LineTotal { get; set; }

    public virtual Herb Herb { get; set; } = null!;

    public virtual PurchaseOrder Po { get; set; } = null!;
}

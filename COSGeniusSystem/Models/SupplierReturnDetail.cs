using System;

namespace COSGeniusSystem.Models;

public partial class SupplierReturnDetail
{
    public int ReturnDetailId { get; set; }

    public int ReturnId { get; set; }

    public int? BatchId { get; set; }

    public int? HerbId { get; set; }

    public string? HerbCode { get; set; }

    public string HerbName { get; set; } = null!;

    public string? SupplierBatchCode { get; set; }

    public DateOnly? ExpiryDate { get; set; }

    public decimal AvailableQuantity { get; set; }

    public decimal ReturnQuantity { get; set; }

    public decimal UnitPrice { get; set; }

    public decimal LineTotal { get; set; }

    public string? Reason { get; set; }

    public string? OriginalReceiptCode { get; set; }

    public virtual SupplierReturn SupplierReturn { get; set; } = null!;
}


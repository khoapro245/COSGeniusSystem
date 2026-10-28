namespace COSGeniusSystem.DTOs.Herbs;

public class BatchResponseDto
{
    public int BatchId { get; set; }
    public string BatchNumber { get; set; } = null!;
    public int WarehouseId { get; set; }
    public string WarehouseName { get; set; } = null!;
    public DateTime ImportDate { get; set; }

    /// <summary>Hạn sử dụng – dùng để sắp xếp FEFO.</summary>
    public DateOnly ExpiryDate { get; set; }

    public decimal OriginalQuantity { get; set; }
    public decimal CurrentQuantity { get; set; }
    public decimal UnitCost { get; set; }
    public string Status { get; set; } = null!;
}

/// <summary>Wrapper trả về danh sách lô kèm tổng tồn kho.</summary>
public class HerbBatchesResponseDto
{
    public int HerbId { get; set; }
    public string HerbCode { get; set; } = null!;
    public string CommonName { get; set; } = null!;
    public string Unit { get; set; } = null!;

    /// <summary>Tổng CurrentQuantity của tất cả các lô đang Active.</summary>
    public decimal TotalCurrentQuantity { get; set; }

    /// <summary>Danh sách lô sắp xếp theo FEFO (ExpiryDate tăng dần).</summary>
    public List<BatchResponseDto> Batches { get; set; } = new();
}

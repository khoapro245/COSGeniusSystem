namespace COSGeniusSystem.DTOs.Herbs;

public class HerbDetailDto
{
    public int HerbId { get; set; }
    public string HerbCode { get; set; } = null!;
    public string CommonName { get; set; } = null!;
    public string? ScientificName { get; set; }

    /// <summary>Đơn vị tính quy chuẩn (g / kg / ml ...).</summary>
    public string Unit { get; set; } = null!;

    /// <summary>Tính chất dược liệu / quy kinh.</summary>
    public string? TasteProperties { get; set; }

    public decimal StandardPrice { get; set; }
    public decimal MinThreshold { get; set; }
    public bool IsActive { get; set; }
}

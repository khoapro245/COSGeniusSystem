namespace COSGeniusSystem.DTOs;

public class HerbResponseDto
{
    public int HerbId { get; set; }
    public string HerbCode { get; set; } = null!;
    public string CommonName { get; set; } = null!;
    public string? ScientificName { get; set; }
    public string Unit { get; set; } = null!;
    public string? TasteProperties { get; set; }
    public decimal StandardPrice { get; set; }
    // [hieppm] Start: Bổ sung LastImportPrice & CurrentStock
    public decimal? LastImportPrice { get; set; }
    public decimal CurrentStock { get; set; }
    // [hieppm] End
    public bool IsActive { get; set; }
}

namespace COSGeniusSystem.DTOs.Herbs;

public class HerbResponseDto
{
    public int HerbId { get; set; }
    public string HerbCode { get; set; } = null!;
    public string CommonName { get; set; } = null!;
    public string? ScientificName { get; set; }
    public string Unit { get; set; } = null!;
    public string? TasteProperties { get; set; }
    public decimal StandardPrice { get; set; }
    public bool IsActive { get; set; }
}

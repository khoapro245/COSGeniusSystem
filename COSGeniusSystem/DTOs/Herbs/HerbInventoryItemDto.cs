namespace COSGeniusSystem.DTOs;

public class HerbInventoryItemDto
{
    public int HerbId { get; set; }
    public string HerbCode { get; set; } = null!;
    public string CommonName { get; set; } = null!;
    public string? ScientificName { get; set; }
    public string Unit { get; set; } = null!;
    public string? TasteProperties { get; set; }
    public decimal StandardPrice { get; set; }
    public decimal MinThreshold { get; set; }
    public bool IsActive { get; set; }
    public decimal TotalStock { get; set; }
    public decimal ActualStock { get; set; }
    public decimal UsableStock { get; set; }
    public decimal DestroyStock { get; set; }
    public string? NearestExpiry { get; set; }
    public string? Location { get; set; }
    public List<BatchResponseDto> Batches { get; set; } = new();
}


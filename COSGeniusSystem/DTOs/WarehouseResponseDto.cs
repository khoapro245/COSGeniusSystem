namespace COSGeniusSystem.DTOs;

public class WarehouseResponseDto
{
    public int WarehouseId { get; set; }
    public string WarehouseCode { get; set; } = string.Empty;
    public string WarehouseName { get; set; } = string.Empty;
    public string? Location { get; set; }
    public bool IsActive { get; set; }
}


namespace COSGeniusSystem.DTOs.TreatmentServices;

public class TreatmentServiceResponseDto
{
    public int ServiceId { get; set; }
    public string ServiceCode { get; set; } = null!;
    public string ServiceName { get; set; } = null!;
    public decimal Price { get; set; }
    public string Unit { get; set; } = null!;
    public string? Description { get; set; }
    public bool IsActive { get; set; }
}

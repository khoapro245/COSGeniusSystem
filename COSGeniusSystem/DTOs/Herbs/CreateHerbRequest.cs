using System.ComponentModel.DataAnnotations;

namespace COSGeniusSystem.DTOs;

public class CreateHerbRequest
{
    [Required(ErrorMessage = "Herb code is required.")]
    public string HerbCode { get; set; } = null!;

    [Required(ErrorMessage = "Herb name is required.")]
    public string CommonName { get; set; } = null!;

    public string? ScientificName { get; set; }

    /// <summary>Đơn vị tính quy chuẩn (g / kg / ml). Bắt buộc.</summary>
    [Required(ErrorMessage = "Unit is required.")]
    public string Unit { get; set; } = null!;

    public string? TasteProperties { get; set; }

    [Required(ErrorMessage = "Standard price is required.")]
    public decimal StandardPrice { get; set; }

    public decimal MinThreshold { get; set; } = 0;
}

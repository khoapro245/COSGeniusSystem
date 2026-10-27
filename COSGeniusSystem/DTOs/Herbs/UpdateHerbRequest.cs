using System.ComponentModel.DataAnnotations;

namespace COSGeniusSystem.DTOs.Herbs;

public class UpdateHerbRequest
{
    [Required(ErrorMessage = "CommonName is required.")]
    public string CommonName { get; set; } = null!;

    public string? ScientificName { get; set; }

    [Required(ErrorMessage = "Unit is required.")]
    public string Unit { get; set; } = null!;

    public string? TasteProperties { get; set; }

    [Required(ErrorMessage = "StandardPrice is required.")]
    public decimal StandardPrice { get; set; }

    public decimal MinThreshold { get; set; }

    public bool IsActive { get; set; }
}

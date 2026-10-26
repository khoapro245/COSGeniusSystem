using System.ComponentModel.DataAnnotations;

namespace COSGeniusSystem.DTOs.TreatmentServices;

public class CreateTreatmentServiceRequest
{
    [Required(ErrorMessage = "ServiceCode is required.")]
    public string ServiceCode { get; set; } = null!;

    [Required(ErrorMessage = "ServiceName is required.")]
    public string ServiceName { get; set; } = null!;

    [Required(ErrorMessage = "Price is required.")]
    public decimal Price { get; set; }

    [Required(ErrorMessage = "Unit is required.")]
    public string Unit { get; set; } = null!;

    public string? Description { get; set; }
}

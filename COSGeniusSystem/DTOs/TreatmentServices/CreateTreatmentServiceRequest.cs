using System.ComponentModel.DataAnnotations;

namespace COSGeniusSystem.DTOs.TreatmentServices;

public class CreateTreatmentServiceRequest
{
    /// <summary>Mã dịch vụ. Nếu để trống, hệ thống sẽ tự động sinh dạng DVxxxxx.</summary>
    [MaxLength(50, ErrorMessage = "ServiceCode must not exceed 50 characters.")]
    public string? ServiceCode { get; set; }

    [Required(ErrorMessage = "ServiceName is required.")]
    [MaxLength(150, ErrorMessage = "ServiceName must not exceed 150 characters.")]
    public string ServiceName { get; set; } = null!;

    [Required(ErrorMessage = "Price is required.")]
    [Range(0.01, double.MaxValue, ErrorMessage = "Price must be greater than 0.")]
    public decimal Price { get; set; }

    [MaxLength(50, ErrorMessage = "Unit must not exceed 50 characters.")]
    public string? Unit { get; set; }

    [MaxLength(255, ErrorMessage = "Description must not exceed 255 characters.")]
    public string? Description { get; set; }

    public bool IsActive { get; set; } = true;
}

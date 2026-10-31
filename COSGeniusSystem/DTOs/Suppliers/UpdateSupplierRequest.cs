using System.ComponentModel.DataAnnotations;

namespace COSGeniusSystem.DTOs.Suppliers;

public class UpdateSupplierRequest
{
    [Required(ErrorMessage = "SupplierName is required.")]
    [MaxLength(200, ErrorMessage = "SupplierName must not exceed 200 characters.")]
    public string SupplierName { get; set; } = null!;

    [MaxLength(20, ErrorMessage = "Phone must not exceed 20 characters.")]
    public string? Phone { get; set; }

    [MaxLength(100, ErrorMessage = "Email must not exceed 100 characters.")]
    [EmailAddress(ErrorMessage = "Email format is invalid.")]
    public string? Email { get; set; }

    [MaxLength(255, ErrorMessage = "Address must not exceed 255 characters.")]
    public string? Address { get; set; }

    [MaxLength(50, ErrorMessage = "TaxCode must not exceed 50 characters.")]
    public string? TaxCode { get; set; }

    public bool IsActive { get; set; } = true;
}


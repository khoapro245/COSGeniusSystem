using System.ComponentModel.DataAnnotations;

namespace COSGeniusSystem.DTOs;

public class CreateSupplierRequest
{
    /// <summary>Mã nhà cung cấp. Nếu để trống hoặc null, hệ thống sẽ tự sinh theo format NCCxxxxx.</summary>
    [MaxLength(50, ErrorMessage = "Supplier code must not exceed 50 characters.")]
    public string? SupplierCode { get; set; }

    [Required(ErrorMessage = "Supplier name is required.")]
    [MaxLength(200, ErrorMessage = "Supplier name must not exceed 200 characters.")]
    public string SupplierName { get; set; } = null!;

    [MaxLength(20, ErrorMessage = "Phone number must not exceed 20 characters.")]
    public string? Phone { get; set; }

    [MaxLength(100, ErrorMessage = "Email must not exceed 100 characters.")]
    [EmailAddress(ErrorMessage = "Invalid email address format.")]
    public string? Email { get; set; }

    [MaxLength(255, ErrorMessage = "Address must not exceed 255 characters.")]
    public string? Address { get; set; }

    [MaxLength(50, ErrorMessage = "Tax code must not exceed 50 characters.")]
    public string? TaxCode { get; set; }

    public bool IsActive { get; set; } = true;
}


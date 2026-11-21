using System.ComponentModel.DataAnnotations;
namespace COSGeniusSystem.DTOs;
public sealed class InvoicePaymentRequest
{
    [Required(ErrorMessage = "This field is required."), MinLength(8, ErrorMessage = "The value is shorter than the minimum allowed length."), MaxLength(8, ErrorMessage = "The value exceeds the maximum allowed length.")] public byte[] RowVersion { get; set; } = [];
    [Required(ErrorMessage = "This field is required.")] public string PaymentMethod { get; set; } = "";
    public bool ReceivedInFull { get; set; }
    public List<int>? InvoiceDetailIds { get; set; }
}
public sealed class InvoiceCancelRequest
{
    [Required(ErrorMessage = "This field is required."), MinLength(8, ErrorMessage = "The value is shorter than the minimum allowed length."), MaxLength(8, ErrorMessage = "The value exceeds the maximum allowed length.")] public byte[] RowVersion { get; set; } = [];
    [Required(ErrorMessage = "This field is required."), StringLength(500, ErrorMessage = "Invalid data length.")] public string Reason { get; set; } = "";
}
public sealed record InvoiceIngredient(string Name, decimal DosagePerDose, int TotalDoses, decimal Quantity, string Unit, decimal UnitPrice, decimal SubTotal);

using System.ComponentModel.DataAnnotations;
namespace COSGeniusSystem.DTOs;
public sealed class InvoicePaymentRequest
{
    [Required, MinLength(8), MaxLength(8)] public byte[] RowVersion { get; set; } = [];
    [Required] public string PaymentMethod { get; set; } = "";
    public bool ReceivedInFull { get; set; }
}
public sealed class InvoiceCancelRequest
{
    [Required, MinLength(8), MaxLength(8)] public byte[] RowVersion { get; set; } = [];
    [Required, StringLength(500)] public string Reason { get; set; } = "";
}
public sealed record InvoiceIngredient(string Name, decimal DosagePerDose, int TotalDoses, decimal Quantity, string Unit, decimal UnitPrice, decimal SubTotal);

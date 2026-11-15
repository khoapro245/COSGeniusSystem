using System.ComponentModel.DataAnnotations;

namespace COSGeniusSystem.DTOs;

public class HerbalistFilter
{
    [StringLength(200, ErrorMessage = "Invalid data length.")] public string? Search { get; set; }
    public string? Status { get; set; }
    public int? DoctorId { get; set; }
    public int? CompletedBy { get; set; }
    public int? PatientId { get; set; }
    public DateTime? FromDate { get; set; }
    public DateTime? ToDate { get; set; }
    [Range(1, int.MaxValue, ErrorMessage = "The value is outside the allowed range.")] public int PageNumber { get; set; } = 1;
    [Range(1, 100, ErrorMessage = "The value is outside the allowed range.")] public int PageSize { get; set; } = 10;
}
public record HerbalistSelection([Required(ErrorMessage = "This field is required."), MinLength(1, ErrorMessage = "The value is shorter than the minimum allowed length."), MaxLength(100, ErrorMessage = "The value exceeds the maximum allowed length.")] int[] PrescriptionIds, [Range(0, int.MaxValue, ErrorMessage = "The value is outside the allowed range.")] int WarehouseId = 0);
public record HerbalistCreateRequest(
    [Required(ErrorMessage = "This field is required."), MinLength(1, ErrorMessage = "The value is shorter than the minimum allowed length."), MaxLength(100, ErrorMessage = "The value exceeds the maximum allowed length.")] int[] PrescriptionIds,
    [StringLength(1000, ErrorMessage = "Invalid data length.")] string? Note, [Range(0, int.MaxValue, ErrorMessage = "The value is outside the allowed range.")] int WarehouseId = 0);
public record HerbalistVersion([Required(ErrorMessage = "This field is required.")] string RowVersion);
public record HerbalistReceive([Required(ErrorMessage = "This field is required.")] string RowVersion,
    [Required(ErrorMessage = "This field is required."), MinLength(1, ErrorMessage = "The value is shorter than the minimum allowed length.")] int[] ConfirmedAllocationIds);
public record ReceiptLine([Range(1,int.MaxValue, ErrorMessage = "The value is outside the allowed range.")] int OutboundDetailId, [Range(typeof(decimal),"0","9999999999")] decimal? ReceivedQuantity, bool ReceiveFull = false);
public record ReceiveHerbs([Required(ErrorMessage = "This field is required.")] string RowVersion, [Required(ErrorMessage = "This field is required."), MinLength(1, ErrorMessage = "The value is shorter than the minimum allowed length."), MaxLength(1000, ErrorMessage = "The value exceeds the maximum allowed length.")] ReceiptLine[] Lines, [StringLength(1000, ErrorMessage = "Invalid data length.")] string? Reason);
public record DispenseAllocation([Range(1,int.MaxValue, ErrorMessage = "The value is outside the allowed range.")] int OutboundDetailId, [Range(typeof(decimal),"0.001","9999999999")] decimal QuantityGrams);
public record CompleteHerbs([Required(ErrorMessage = "This field is required.")] string RowVersion, [Required(ErrorMessage = "This field is required."), MinLength(1, ErrorMessage = "The value is shorter than the minimum allowed length."), MaxLength(1000, ErrorMessage = "The value exceeds the maximum allowed length.")] DispenseAllocation[] Allocations);



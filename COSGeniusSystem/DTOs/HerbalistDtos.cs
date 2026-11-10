using System.ComponentModel.DataAnnotations;

namespace COSGeniusSystem.DTOs;

public class HerbalistFilter
{
    [StringLength(200)] public string? Search { get; set; }
    public string? Status { get; set; }
    public int? DoctorId { get; set; }
    public int? CompletedBy { get; set; }
    public int? PatientId { get; set; }
    public DateTime? FromDate { get; set; }
    public DateTime? ToDate { get; set; }
    [Range(1, int.MaxValue)] public int PageNumber { get; set; } = 1;
    [Range(1, 100)] public int PageSize { get; set; } = 20;
}
public record HerbalistSelection([Required, MinLength(1), MaxLength(100)] int[] PrescriptionIds, [Range(0, int.MaxValue)] int WarehouseId = 0);
public record HerbalistCreateRequest(
    [Required, MinLength(1), MaxLength(100)] int[] PrescriptionIds,
    [StringLength(1000)] string? Note, [Range(0, int.MaxValue)] int WarehouseId = 0);
public record HerbalistVersion([Required] string RowVersion);
public record HerbalistReceive([Required] string RowVersion,
    [Required, MinLength(1)] int[] ConfirmedAllocationIds);
public record ReceiptLine([Range(1,int.MaxValue)] int OutboundDetailId, [Range(typeof(decimal),"0","9999999999")] decimal? ReceivedQuantity, bool ReceiveFull = false);
public record ReceiveHerbs([Required] string RowVersion, [Required, MinLength(1), MaxLength(1000)] ReceiptLine[] Lines, [StringLength(1000)] string? Reason);
public record DispenseAllocation([Range(1,int.MaxValue)] int OutboundDetailId, [Range(typeof(decimal),"0.01","9999999999")] decimal QuantityGrams);
public record CompleteHerbs([Required] string RowVersion, [Required, MinLength(1), MaxLength(1000)] DispenseAllocation[] Allocations);



namespace COSGeniusSystem.Models;

public class HerbRequisition
{
 public int Id { get; set; }
 public string RequisitionCode { get; set; } = "RQ" + Guid.NewGuid().ToString("N");
 public int WarehouseId { get; set; }
 public int CreatedBy { get; set; }
 public DateTime CreatedAt { get; set; }
 public string Status { get; set; } = "REQUESTED";
 public string? Note { get; set; }
 public int? ReceivedBy { get; set; }
 public DateTime? ReceivedAt { get; set; }
 public int? IssuedBy { get; set; }
 public DateTime? IssuedAt { get; set; }
 public int? ClosedBy { get; set; }
 public DateTime? ClosedAt { get; set; }
 public string? RejectionReason { get; set; }
 public byte[] RowVersion { get; set; } = [];
 public List<HerbRequisitionPrescription> Prescriptions { get; set; } = [];
 public List<HerbOutboundDetail> Allocations { get; set; } = [];
 public List<HerbRequisitionDetail> Details { get; set; } = [];
}
public class HerbRequisitionPrescription
{
 public int Id { get; set; }
 public int RequestId { get; set; }
 public int PrescriptionId { get; set; }
 public bool IsActive { get; set; } = true;
 public int? CompletedBy { get; set; }
 public DateTime? CompletedAt { get; set; }
 public HerbRequisition Request { get; set; } = null!;
 public HerbalPrescription Prescription { get; set; } = null!;
}
public class HerbRequisitionDetail
{
 public int Id { get; set; }
 public int RequestId { get; set; }
 public int PrescriptionId { get; set; }
 public int PrescriptionDetailId { get; set; }
 public int HerbId { get; set; }
 // Workflow quantities are grams. Batch quantities remain kg.
 public decimal RequestedQuantity { get; set; }
 public string UnitSnapshot { get; set; } = "g";
 public HerbRequisition Request { get; set; } = null!;
}
public class HerbOutbound
{
 public int Id { get; set; }
 public string OutboundCode { get; set; } = "OUT" + Guid.NewGuid().ToString("N");
 public int RequestId { get; set; }
 public int WarehouseId { get; set; }
 public int IssuedBy { get; set; }
 public DateTime IssuedAt { get; set; }
 public int? ReceivedBy { get; set; }
 public DateTime? ReceivedAt { get; set; }
 public string ReceiptStatus { get; set; } = "PENDING";
 public List<HerbOutboundDetail> Details { get; set; } = [];
}
public class HerbOutboundDetail
{
 public int Id { get; set; }
 public int OutboundId { get; set; }
 public int RequestId { get; set; }
 public int WarehouseId { get; set; }
 public int HerbId { get; set; }
 public int BatchId { get; set; }
 public decimal QuantityGrams { get; set; }
 public decimal? ReceivedQuantity { get; set; }
 public int? ReceivedBy { get; set; }
 public DateTime? ReceivedAt { get; set; }
 public int? StorageLocationId { get; set; }
 public string? LocationSnapshot { get; set; }
 public HerbRequisition Request { get; set; } = null!;
 public HerbOutbound Outbound { get; set; } = null!;
 public Batch Batch { get; set; } = null!;
}
public class WarehouseStorageLocation
{
 public int Id { get; set; }
 public int WarehouseId { get; set; }
 public string Code { get; set; } = "";
 public bool IsActive { get; set; } = true;
}
public class HerbReceiptDiscrepancy
{
 public int Id { get; set; }
 public string ReportCode { get; set; } = "DIF" + Guid.NewGuid().ToString("N");
 public int OutboundId { get; set; }
 public int ReportedBy { get; set; }
 public DateTime ReportedAt { get; set; }
 public string Content { get; set; } = "";
 public string Status { get; set; } = "OPEN";
 public List<HerbReceiptDiscrepancyDetail> Details { get; set; } = [];
}
public class HerbReceiptDiscrepancyDetail
{
 public int Id { get; set; }
 public int ReportId { get; set; }
 public int OutboundId { get; set; }
 public int OutboundDetailId { get; set; }
 public decimal ExpectedQuantity { get; set; }
 public decimal ReceivedQuantity { get; set; }
 public decimal DifferenceQuantity { get; set; }
 public string Reason { get; set; } = "";
}


public class HerbOutboundDemand
{
 public int Id { get; set; }
 public int OutboundDetailId { get; set; }
 public int RequisitionDetailId { get; set; }
 public decimal QuantityGrams { get; set; }
 public HerbOutboundDetail OutboundDetail { get; set; } = null!;
}

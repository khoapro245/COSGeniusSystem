namespace COSGeniusSystem.DTOs;

public class DispensingSlipFilterQuery
{
    public string? Keyword { get; set; }
    public string? Status { get; set; }
    public DateTime? FromDate { get; set; }
    public DateTime? ToDate { get; set; }
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 20;
}

public class DispensingSlipItemDto
{
    public int SlipId { get; set; }
    public string SlipCode { get; set; } = null!;
    public string CreatedAt { get; set; } = null!;
    public string CreatedBy { get; set; } = null!;
    public int OrderCount { get; set; }
    public decimal TotalQuantityG { get; set; }
    public string Status { get; set; } = null!;
    public string? RejectReason { get; set; }
}

public class HerbCheckItemDto
{
    public string Id { get; set; } = null!;
    public int HerbId { get; set; }
    public string HerbName { get; set; } = null!;
    public decimal RequiredG { get; set; }
    public decimal ActualG { get; set; }
    public string BatchCode { get; set; } = null!;
    public bool Verified { get; set; }
}

public class OrderPrescriptionItemDto
{
    public string HerbName { get; set; } = null!;
    public decimal DosagePerDose { get; set; }
    public decimal TotalDoseG { get; set; }
}

public class OrderInSlipDto
{
    public int OrderId { get; set; }
    public string OrderCode { get; set; } = null!;
    public string PatientName { get; set; } = null!;
    public string PatientCode { get; set; } = null!;
    public string DoctorName { get; set; } = null!;
    public int Doses { get; set; }
    public string Status { get; set; } = null!;
    public string? CompletedAt { get; set; }
    public List<OrderPrescriptionItemDto> Items { get; set; } = new();
}

public class DispensingSlipDetailDto
{
    public int SlipId { get; set; }
    public string SlipCode { get; set; } = null!;
    public string CreatedBy { get; set; } = null!;
    public string CreatedAt { get; set; } = null!;
    public int OrderCount { get; set; }
    public string Status { get; set; } = null!;
    public string? WarehouseStaffInfo { get; set; }
    public List<HerbCheckItemDto> Herbs { get; set; } = new();
    public List<OrderInSlipDto> Orders { get; set; } = new();
}

public class DispenseHistoryFilterQuery
{
    public string? Keyword { get; set; }
    public int? DispenserId { get; set; }
    public DateTime? FromDate { get; set; }
    public DateTime? ToDate { get; set; }
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 20;
}

public class DispensedHistoryRecordDto
{
    public int OrderId { get; set; }
    public string OrderCode { get; set; } = null!;
    public string PatientName { get; set; } = null!;
    public string PatientCode { get; set; } = null!;
    public string SlipCode { get; set; } = null!;
    public int Doses { get; set; }
    public string Dispenser { get; set; } = null!;
    public string CompletedAt { get; set; } = null!;
    public string DoctorName { get; set; } = null!;
    public List<OrderPrescriptionItemDto> Items { get; set; } = new();
}

// ─── DTOs cho HB-01 & HB-03 / HB-04 ───

public class PendingPrescriptionDto
{
    public int OrderId { get; set; }
    public string OrderCode { get; set; } = null!;
    public string PatientName { get; set; } = null!;
    public string PatientCode { get; set; } = null!;
    public string DoctorName { get; set; } = null!;
    public int Doses { get; set; }
    public string Status { get; set; } = null!;
    public string StatusDisplay { get; set; } = null!;
    public string? LinkedSlipCode { get; set; }
    public List<OrderPrescriptionItemDto> Items { get; set; } = new();
}

public class AggregateHerbCheckDto
{
    public int HerbId { get; set; }
    public string HerbName { get; set; } = null!;
    public decimal TotalG { get; set; }
    public decimal NeedKg { get; set; }
    public decimal StockKg { get; set; }
    public bool IsEnough { get; set; }
    public decimal ShortageG { get; set; }
    public Dictionary<string, decimal> OrdersG { get; set; } = new();
}

public class AggregateSlipPreviewDto
{
    public List<PendingPrescriptionDto> Orders { get; set; } = new();
    public List<AggregateHerbCheckDto> HerbAggregates { get; set; } = new();
    public bool AllHerbsInStock { get; set; }
}

public class CreateDispensingSlipRequest
{
    public List<int> OrderIds { get; set; } = new();
    public List<int> PrescriptionIds { get => OrderIds; set => OrderIds = value; }
    public string? Note { get; set; }
}

// ─── DTOs cho Nghiệp vụ Xuất kho (Warehouse Issue & FEFO) ───

public class BatchAllocationItem
{
    public int BatchId { get; set; }
    public decimal QuantityGrams { get; set; }
}

public class WarehouseIssueRequest
{
    public List<BatchAllocationItem> Allocations { get; set; } = new();
}

public class WarehouseRejectRequest
{
    public string Reason { get; set; } = null!;
}

// ─── DTOs cho bốc thuốc (HB-07) ───

public class DispenseBatchLineRequest
{
    public int BatchId { get; set; }
    public decimal QuantityGrams { get; set; }
}

public class CompleteDispenseOrderRequest
{
    // Frontend gửi lên danh sách lô + lượng thực tế bốc cho đơn này.
    // Nếu không gửi, server sẽ dùng FEFO tự động.
    public List<DispenseBatchLineRequest>? BatchLines { get; set; }
}

public class FefoSuggestedLineDto
{
    public int BatchId { get; set; }
    public string BatchNumber { get; set; } = null!;
    public string ExpiryDate { get; set; } = null!;
    public string Location { get; set; } = null!;
    public decimal QuantityGrams { get; set; }
    public decimal AvailableGrams { get; set; }
}

public class FefoHerbSuggestionDto
{
    public int HerbId { get; set; }
    public string HerbName { get; set; } = null!;
    public decimal RequiredGrams { get; set; }
    public decimal ShortageGrams { get; set; }
    public List<FefoSuggestedLineDto> Lines { get; set; } = new();
}

using System.ComponentModel.DataAnnotations;

namespace COSGeniusSystem.DTOs;

// [hieppm] Start: Định nghĩa hằng số trạng thái phiếu nhập kho chuẩn theo Database
public static class GoodsReceiptStatusConstants
{
    public const string PendingApproval = "PENDING_APPROVAL";
    public const string Approved = "APPROVED";
    public const string Rejected = "REJECTED";
}
// [hieppm] End: Định nghĩa hằng số trạng thái phiếu nhập kho chuẩn theo Database

public class ReceiptDetailDto
{
    public int ReceiptDetailId { get; set; }
    public int ReceiptId { get; set; }
    public int BatchId { get; set; }
    public string BatchNumber { get; set; } = string.Empty;
    public int HerbId { get; set; }
    public string HerbCode { get; set; } = string.Empty;
    public string HerbName { get; set; } = string.Empty;
    public string? SupplierBatchCode { get; set; }
    public DateOnly ExpiryDate { get; set; }
    public decimal Quantity { get; set; }
    public decimal UnitPrice { get; set; }
    public decimal LineTotal { get; set; }
    // [hieppm] Start: Bổ sung StorageLocationId & StorageLocationCode
    public int? StorageLocationId { get; set; }
    public string? StorageLocationCode { get; set; }
    public string? Location { get; set; }
    // [hieppm] End
}

public class GoodsReceiptDto
{
    public int ReceiptId { get; set; }
    public string ReceiptCode { get; set; } = string.Empty;
    public int? Poid { get; set; }
    public string? Pocode { get; set; }
    public string SupplierName { get; set; } = string.Empty;
    public int WarehouseId { get; set; }
    public string WarehouseName { get; set; } = string.Empty;
    public int ReceivedBy { get; set; }
    public string ReceiverName { get; set; } = string.Empty;
    public DateTime ReceiptDate { get; set; }
    public decimal TotalCost { get; set; }
    public decimal TotalQuantity { get; set; }
    public string Status { get; set; } = "PENDING_APPROVAL";
    public string StatusLabel { get; set; } = "Chờ quản lý duyệt";
    public string? Note { get; set; }
    public string? ProcessLog { get; set; }
    // [hieppm] Start: Bổ sung DriverName, VehicleNumber, DocumentUrl
    public string? DriverName { get; set; }
    public string? VehicleNumber { get; set; }
    public string? DocumentUrl { get; set; }
    // [hieppm] End
    public List<ReceiptDetailDto> Details { get; set; } = new();
}

public class CreateGoodsReceiptItemRequest
{
    [Required(ErrorMessage = "Herb is required.")]
    public int HerbId { get; set; }

    [Required(ErrorMessage = "Supplier batch code is required.")]
    public string SupplierBatchCode { get; set; } = string.Empty;

    [Required(ErrorMessage = "Expiry date is required.")]
    public DateOnly ExpiryDate { get; set; }

    [Range(0.001, double.MaxValue, ErrorMessage = "Received quantity must be greater than 0.")]
    public decimal Quantity { get; set; }

    [Range(0, double.MaxValue, ErrorMessage = "Unit price must be greater than or equal to 0.")]
    public decimal UnitPrice { get; set; }

    // [hieppm] Start: Bổ sung StorageLocationId
    public int? StorageLocationId { get; set; }
    public string? Location { get; set; }
    // [hieppm] End
}

public class CreateGoodsReceiptRequest
{
    public int? Poid { get; set; }

    [Required(ErrorMessage = "Receiving warehouse is required.")]
    public int WarehouseId { get; set; } = 1;

    public string? Note { get; set; }

    // [hieppm] Start: Bổ sung DriverName, VehicleNumber, DocumentUrl
    public string? DriverName { get; set; }
    public string? VehicleNumber { get; set; }
    public string? DocumentUrl { get; set; }
    // [hieppm] End

    [MinLength(1, ErrorMessage = "Phiếu nhập kho phải có ít nhất 1 dòng vị thuốc.")]
    public List<CreateGoodsReceiptItemRequest> Items { get; set; } = new();
}

public class ApproveGoodsReceiptItemRequest
{
    public int? BatchId { get; set; }
    public int? HerbId { get; set; }
    public string? SupplierBatchCode { get; set; }
    public decimal? UnitPrice { get; set; }
    public string? Location { get; set; }
    public int? StorageLocationId { get; set; }
}

public class ApproveGoodsReceiptRequest
{
    public bool IsApproved { get; set; } = true;
    public string? ManagerNote { get; set; }
    // [hieppm] Start: Bổ sung danh sách vị thuốc/lô hàng kèm đơn giá và vị trí lưu kho khi Manager duyệt
    public List<ApproveGoodsReceiptItemRequest>? Items { get; set; }
    // [hieppm] End
}

// [hieppm] Start: DTO gợi ý vị trí kệ kho và upload chứng từ
public class StorageLocationSuggestionDto
{
    public int Id { get; set; }
    public string Code { get; set; } = string.Empty;
    public string StorageType { get; set; } = "STANDARD"; // STANDARD | COLD
    public decimal MaxCapacityGrams { get; set; }
    public decimal CurrentUsedGrams { get; set; }
    public decimal RemainingCapacityGrams { get; set; }
    public bool IsSuitable { get; set; } = true;
}

public class DocumentUploadResponseDto
{
    public string FileUrl { get; set; } = string.Empty;
    public string FileName { get; set; } = string.Empty;
}
// [hieppm] End


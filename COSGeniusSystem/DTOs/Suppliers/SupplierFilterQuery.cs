namespace COSGeniusSystem.DTOs.Suppliers;

public class SupplierFilterQuery
{
    /// <summary>Tìm kiếm tương đối theo SupplierCode, SupplierName, Phone, Email, TaxCode.</summary>
    public string? Keyword { get; set; }

    /// <summary>Lọc theo trạng thái IsActive. Để trống để lấy tất cả.</summary>
    public bool? IsActive { get; set; }

    public int Page { get; set; } = 1;

    public int PageSize { get; set; } = 20;
}


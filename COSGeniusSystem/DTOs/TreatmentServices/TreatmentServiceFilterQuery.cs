namespace COSGeniusSystem.DTOs;

public class TreatmentServiceFilterQuery
{
    /// <summary>Tìm kiếm theo mã dịch vụ, tên dịch vụ hoặc mô tả.</summary>
    public string? Keyword { get; set; }

    /// <summary>Lọc theo trạng thái hoạt động.</summary>
    public bool? IsActive { get; set; }

    /// <summary>Nếu true và IsActive == null, sẽ trả về cả active và inactive (dành cho quản lý).</summary>
    public bool? IncludeInactive { get; set; }

    public int Page { get; set; } = 1;

    public int PageSize { get; set; } = 10;
}


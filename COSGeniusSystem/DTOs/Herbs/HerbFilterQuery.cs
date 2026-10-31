namespace COSGeniusSystem.DTOs.Herbs;

public class HerbFilterQuery
{
    /// <summary>Tìm kiếm tương đối theo CommonName hoặc HerbCode (không phân biệt hoa thường).</summary>
    public string? Keyword { get; set; }

    /// <summary>Lọc theo TasteProperties (tính chất / quy kinh) – contains.</summary>
    public string? TasteProperties { get; set; }

    /// <summary>Mặc định lấy true. Client truyền null hoặc false để lọc tương ứng.</summary>
    public bool? IsActive { get; set; } = true;

    // ---------- Phân trang ----------
    /// <summary>Trang hiện tại (bắt đầu từ 1).</summary>
    [System.ComponentModel.DataAnnotations.Range(1, 1000000)]
    public int Page { get; set; } = 1;

    /// <summary>Số bản ghi mỗi trang.</summary>
    [System.ComponentModel.DataAnnotations.Range(1, 100)]
    public int PageSize { get; set; } = 20;
}

namespace COSGeniusSystem.DTOs.Doctors;

public class DoctorFilterQuery
{
    /// <summary>Tìm kiếm tương đối theo FullName hoặc Phone (không phân biệt hoa thường).</summary>
    public string? Keyword { get; set; }

    /// <summary>Lọc theo chuyên khoa/chuyên môn.</summary>
    public string? Specialization { get; set; }

    /// <summary>Mặc định lấy true. Client truyền null hoặc false để lọc tương ứng.</summary>
    public bool? IsActive { get; set; } = true;
}

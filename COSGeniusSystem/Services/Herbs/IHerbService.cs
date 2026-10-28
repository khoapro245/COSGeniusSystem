using COSGeniusSystem.DTOs.Herbs;

namespace COSGeniusSystem.Services.Herbs;

public interface IHerbService
{
    /// <summary>
    /// API-TU-01: Lấy danh sách vị thuốc có lọc và phân trang.
    /// </summary>
    Task<List<HerbResponseDto>> GetHerbsAsync(HerbFilterQuery query);

    /// <summary>
    /// API-TU-02: Xem chi tiết vị thuốc theo Id.
    /// Trả về null nếu không tìm thấy.
    /// </summary>
    Task<HerbDetailDto?> GetHerbByIdAsync(int id);

    /// <summary>
    /// API-TU-03: Thêm mới vị thuốc vào danh mục.
    /// Trả về ErrorMessage nếu HerbCode/CommonName trùng hoặc StandardPrice &lt;= 0.
    /// </summary>
    Task<(HerbResponseDto? Dto, string? ErrorMessage)> CreateHerbAsync(CreateHerbRequest request);

    /// <summary>
    /// API-TU-04: Cập nhật thông tin vị thuốc theo id.
    /// Trả về (null, null) nếu không tìm thấy → 404.
    /// Trả về (null, errorMessage) nếu lỗi validation → 400.
    /// </summary>
    Task<(HerbResponseDto? Dto, string? ErrorMessage)> UpdateHerbAsync(int id, UpdateHerbRequest request);

    /// <summary>
    /// API-TU-05: Lấy danh sách lô tồn kho của một vị thuốc (FEFO).
    /// Trả về null nếu HerbId không tồn tại.
    /// </summary>
    Task<HerbBatchesResponseDto?> GetHerbBatchesAsync(int herbId);
}

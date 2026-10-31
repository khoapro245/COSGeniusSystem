using COSGeniusSystem.DTOs.TreatmentServices;

namespace COSGeniusSystem.Services.TreatmentServices;

public interface ITreatmentService
{
    /// <summary>Lấy danh sách dịch vụ đang hoạt động (IsActive == true).</summary>
    Task<List<TreatmentServiceResponseDto>> GetActiveServicesAsync(CancellationToken ct = default);

    /// <summary>
    /// Thêm mới dịch vụ. Trả về null nếu ServiceCode hoặc ServiceName đã tồn tại,
    /// hoặc nếu Price &lt;= 0.
    /// </summary>
    Task<(TreatmentServiceResponseDto? Dto, string? ErrorMessage)> CreateServiceAsync(CreateTreatmentServiceRequest request);

    /// <summary>
    /// Cập nhật dịch vụ theo id. Trả về null Dto nếu không tìm thấy,
    /// hoặc ErrorMessage nếu có lỗi validation/trùng tên.
    /// </summary>
    Task<(TreatmentServiceResponseDto? Dto, string? ErrorMessage)> UpdateServiceAsync(int id, UpdateTreatmentServiceRequest request);
}

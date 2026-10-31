using COSGeniusSystem.DTOs.TreatmentServices;

namespace COSGeniusSystem.Services.TreatmentServices;

public interface ITreatmentService
{
    /// <summary>Lấy danh sách dịch vụ đang hoạt động (IsActive == true) - phục vụ lâm sàng.</summary>
    Task<List<TreatmentServiceResponseDto>> GetActiveServicesAsync(CancellationToken ct = default);

    /// <summary>Lấy danh sách dịch vụ có lọc và tìm kiếm - phục vụ quản lý.</summary>
    Task<List<TreatmentServiceResponseDto>> GetServicesAsync(TreatmentServiceFilterQuery query, CancellationToken ct = default);

    /// <summary>Lấy chi tiết dịch vụ theo ID.</summary>
    Task<TreatmentServiceResponseDto?> GetServiceByIdAsync(int id, CancellationToken ct = default);

    /// <summary>Lấy chi tiết dịch vụ theo mã.</summary>
    Task<TreatmentServiceResponseDto?> GetServiceByCodeAsync(string code, CancellationToken ct = default);

    /// <summary>Thêm mới dịch vụ.</summary>
    Task<(TreatmentServiceResponseDto? Dto, string? ErrorMessage)> CreateServiceAsync(CreateTreatmentServiceRequest request, CancellationToken ct = default);

    /// <summary>Cập nhật dịch vụ.</summary>
    Task<(TreatmentServiceResponseDto? Dto, string? ErrorMessage)> UpdateServiceAsync(int id, UpdateTreatmentServiceRequest request, CancellationToken ct = default);

    /// <summary>Xóa hoặc ngưng hoạt động dịch vụ.</summary>
    Task<(bool Success, bool IsDeactivated, string? ErrorMessage)> DeleteServiceAsync(int id, CancellationToken ct = default);
}

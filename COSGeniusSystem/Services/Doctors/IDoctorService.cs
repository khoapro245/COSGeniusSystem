using COSGeniusSystem.DTOs;

namespace COSGeniusSystem.Services;

public interface IDoctorService
{
    /// <summary>Lấy danh sách bác sĩ có lọc theo Keyword, Specialization và IsActive.</summary>
    Task<List<DoctorResponseDto>> GetDoctorsAsync(DoctorFilterQuery query, CancellationToken ct = default);

    /// <summary>
    /// Xem chi tiết bác sĩ theo Id (kèm thông tin User liên kết).
    /// Trả về null nếu không tìm thấy.
    /// </summary>
    Task<DoctorDetailDto?> GetDoctorByIdAsync(int id);

    /// <summary>
    /// Lấy toàn bộ dữ liệu tổng quan phòng khám & danh sách bác sĩ (tối ưu 1 endpoint duy nhất cho Tiếp tân).
    /// </summary>
    Task<DoctorClinicOverviewDto> GetDoctorClinicOverviewAsync(DoctorClinicFilterQuery query, CancellationToken ct = default);
}

using COSGeniusSystem.DTOs.Doctors;

namespace COSGeniusSystem.Services.Doctors;

public interface IDoctorService
{
    /// <summary>Lấy danh sách bác sĩ có lọc theo Keyword, Specialization và IsActive.</summary>
    Task<List<DoctorResponseDto>> GetDoctorsAsync(DoctorFilterQuery query);

    /// <summary>
    /// Xem chi tiết bác sĩ theo Id (kèm thông tin User liên kết).
    /// Trả về null nếu không tìm thấy.
    /// </summary>
    Task<DoctorDetailDto?> GetDoctorByIdAsync(int id);
}

using COSGeniusSystem.Data;
using COSGeniusSystem.DTOs.Doctors;
using Microsoft.EntityFrameworkCore;

namespace COSGeniusSystem.Services.Doctors;

public class DoctorService : IDoctorService
{
    private readonly ApplicationDbContext _context;

    public DoctorService(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<List<DoctorResponseDto>> GetDoctorsAsync(DoctorFilterQuery query)
    {
        var q = _context.Doctors.AsNoTracking();

        // Lọc IsActive (mặc định true)
        if (query.IsActive.HasValue)
            q = q.Where(d => d.IsActive == query.IsActive.Value);

        // Lọc theo Specialization
        if (!string.IsNullOrWhiteSpace(query.Specialization))
            q = q.Where(d => d.Specialization != null
                && d.Specialization.ToLower().Contains(query.Specialization.ToLower()));

        // Tìm kiếm tương đối theo FullName hoặc Phone (không phân biệt hoa thường)
        if (!string.IsNullOrWhiteSpace(query.Keyword))
        {
            var keyword = query.Keyword.ToLower();
            q = q.Where(d =>
                d.FullName.ToLower().Contains(keyword) ||
                (d.Phone != null && d.Phone.ToLower().Contains(keyword)));
        }

        return await q.Select(d => new DoctorResponseDto
        {
            DoctorId = d.DoctorId,
            FullName = d.FullName,
            Specialization = d.Specialization,
            Phone = d.Phone,
            IsActive = d.IsActive
        }).ToListAsync();
    }

    public async Task<DoctorDetailDto?> GetDoctorByIdAsync(int id)
    {
        var doctor = await _context.Doctors
            .AsNoTracking()
            .Include(d => d.User)
            .FirstOrDefaultAsync(d => d.DoctorId == id);

        if (doctor is null)
            return null;

        return new DoctorDetailDto
        {
            DoctorId = doctor.DoctorId,
            FullName = doctor.FullName,
            Specialization = doctor.Specialization,
            LicenseNumber = doctor.LicenseNumber,
            Phone = doctor.Phone,
            IsActive = doctor.IsActive,
            // Thông tin user liên kết (có thể null nếu bác sĩ chưa có tài khoản)
            Username = doctor.User?.Username,
            Email = doctor.User?.Email
        };
    }
}

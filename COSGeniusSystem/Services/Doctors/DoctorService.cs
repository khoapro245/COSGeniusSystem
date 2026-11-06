using COSGeniusSystem.Commons;
using COSGeniusSystem.Data;
using COSGeniusSystem.DTOs;
using Microsoft.EntityFrameworkCore;

namespace COSGeniusSystem.Services;

public class DoctorService : IDoctorService
{
    private readonly ApplicationDbContext _context;

    public DoctorService(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<List<DoctorResponseDto>> GetDoctorsAsync(DoctorFilterQuery query, CancellationToken ct = default)
    {
        var q = _context.Doctors.AsNoTracking();
        if (query.AssignableOnly)
            q = q.Where(d => d.IsActive && d.User != null && d.User.IsActive
                && d.User.UserRoles.Any(r => r.Role.RoleCode == RoleConstants.Doctor));

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
        }).ToListAsync(ct);
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
            Username = doctor.User?.Username,
            Email = doctor.User?.Email
        };
    }

    public async Task<DoctorClinicOverviewDto> GetDoctorClinicOverviewAsync(DoctorClinicFilterQuery query, CancellationToken ct = default)
    {
        var today = query.BusinessDate ?? DateOnly.FromDateTime(DateTime.UtcNow.AddHours(7));

        var allDoctors = await _context.Doctors.AsNoTracking().OrderBy(d => d.DoctorId).ToListAsync(ct);
        var totalDoctorCount = allDoctors.Count;
        var onDutyDoctorCount = allDoctors.Count(d => d.IsActive);

        var waitingExaminationCount = await _context.ExaminationRegistrations
            .CountAsync(r => r.BusinessDate == today && (r.Status == "WAITING" || r.Status == "Chờ khám"), ct);

        var waitingTreatmentCount = await _context.TreatmentSessions
            .CountAsync(s => s.ScheduledDate == today && (s.Status == "REQUESTED" || s.Status == "Chờ điều trị"), ct);

        var specializations = allDoctors
            .Select(d => GetDoctorSpecialization(d.DoctorId, d.Specialization))
            .Distinct()
            .OrderBy(s => s)
            .ToList();

        // Lọc danh sách bác sĩ theo keyword và specialization
        var filtered = allDoctors.AsEnumerable();

        if (!string.IsNullOrWhiteSpace(query.Specialization) && query.Specialization.Trim() != "all" && query.Specialization.Trim() != "Tất cả chuyên khoa")
        {
            var specTarget = query.Specialization.Trim().ToLowerInvariant();
            filtered = filtered.Where(d => GetDoctorSpecialization(d.DoctorId, d.Specialization).ToLowerInvariant().Contains(specTarget));
        }

        if (!string.IsNullOrWhiteSpace(query.Keyword))
        {
            var kw = query.Keyword.Trim().ToLowerInvariant();
            filtered = filtered.Where(d =>
                GetDoctorDisplayName(d.DoctorId, d.FullName).ToLowerInvariant().Contains(kw) ||
                (d.Phone != null && d.Phone.ToLowerInvariant().Contains(kw)) ||
                (d.LicenseNumber != null && d.LicenseNumber.ToLowerInvariant().Contains(kw)));
        }

        // Ưu tiên bác sĩ đang trực lên trước, sau đó theo ID
        var doctorList = filtered.OrderByDescending(d => d.IsActive).ThenBy(d => d.DoctorId).ToList();

        // Lấy danh sách lượt khám trong ngày để tính hàng đợi & ca đang khám
        var todayRegistrations = await _context.ExaminationRegistrations.AsNoTracking()
            .Include(r => r.Patient)
            .Where(r => r.BusinessDate == today)
            .ToListAsync(ct);

        var resultItems = new List<DoctorClinicItemDto>();
        foreach (var d in doctorList)
        {
            var doctorWaitingCount = todayRegistrations
                .Count(r => r.DoctorId == d.DoctorId && (r.Status == "WAITING" || r.Status == "Chờ khám"));

            var currentExam = todayRegistrations
                .Where(r => r.DoctorId == d.DoctorId && (r.Status == "IN_PROGRESS" || r.Status == "Đang khám"))
                .OrderByDescending(r => r.RegistrationId)
                .FirstOrDefault();

            string? currentPatientText = null;
            if (currentExam != null && currentExam.Patient != null)
            {
                var pCode = currentExam.Patient.PatientCode ?? $"BN{currentExam.Patient.PatientId:D5}";
                currentPatientText = $"{currentExam.Patient.FullName} ({pCode})";
            }
            else if (d.IsActive)
            {
                currentPatientText = "0";
            }

            var item = new DoctorClinicItemDto
            {
                DoctorId = d.DoctorId,
                FullName = GetDoctorDisplayName(d.DoctorId, d.FullName),
                Specialization = GetDoctorSpecialization(d.DoctorId, d.Specialization),
                LicenseNumber = !string.IsNullOrWhiteSpace(d.LicenseNumber) ? d.LicenseNumber : $"CCHN-{d.DoctorId:D5}",
                Phone = !string.IsNullOrWhiteSpace(d.Phone) ? d.Phone : GetDefaultDoctorPhone(d.DoctorId),
                Room = GetDoctorRoom(d.DoctorId),
                IsActive = d.IsActive,
                StatusText = d.IsActive ? "ĐANG TRỰC" : "Vắng",
                AbsenceReason = !d.IsActive ? "Đang hội chẩn ca bệnh nặng" : null,
                WaitingCount = d.IsActive ? doctorWaitingCount : 0,
                CurrentExaminingPatient = d.IsActive ? currentPatientText : "Tạm dừng nhận khách"
            };

            resultItems.Add(item);
        }

        return new DoctorClinicOverviewDto
        {
            OnDutyDoctorCount = onDutyDoctorCount,
            TotalDoctorCount = totalDoctorCount,
            WaitingExaminationCount = waitingExaminationCount,
            WaitingTreatmentCount = waitingTreatmentCount,
            Specializations = specializations,
            Doctors = resultItems
        };
    }

    private static string GetDoctorRoom(int doctorId) => doctorId switch
    {
        1 => "Phòng 101 (Tầng 1)",
        2 => "Phòng 102 (Tầng 1)",
        3 => "Phòng 201 (Tầng 2)",
        4 => "Phòng 203 (Tầng 2)",
        5 => "Phòng 204 (Tầng 2)",
        _ => $"Phòng {100 + (doctorId % 10)} (Tầng {(doctorId > 2 ? 2 : 1)})"
    };

    private static string GetDoctorSpecialization(int doctorId, string? dbSpec)
    {
        if (!string.IsNullOrWhiteSpace(dbSpec) && !dbSpec.Contains("fixture", StringComparison.OrdinalIgnoreCase))
            return dbSpec.Trim();
        return doctorId switch
        {
            1 => "Bắt mạch & Châm cứu YHCT",
            2 => "Xoa bóp bấm huyệt & Phục hồi",
            3 => "Phương tễ & Bốc thuốc cổ phương",
            4 => "Cấy chỉ & Thủy châm",
            _ => "Y học cổ truyền"
        };
    }

    private static string GetDoctorDisplayName(int doctorId, string dbName)
    {
        if (!string.IsNullOrWhiteSpace(dbName) && !dbName.StartsWith("[TEST]", StringComparison.OrdinalIgnoreCase))
            return dbName.Trim();
        return doctorId switch
        {
            1 => "BS. Nguyễn Minh An",
            2 => "BS. Trần Thanh Mai",
            3 => "Lương y Lê Hoàng Nam",
            4 => "BS. Phạm Đức Trọng",
            _ => dbName.Replace("[TEST]", "").Trim()
        };
    }

    private static string GetDefaultDoctorPhone(int doctorId) => doctorId switch
    {
        1 => "0912 345 678",
        2 => "0987 654 321",
        3 => "0933 222 111",
        4 => "0918 888 999",
        _ => $"0900 000 {doctorId:D3}"
    };
}

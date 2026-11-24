using COSGeniusSystem.Commons;
using COSGeniusSystem.Data;
using COSGeniusSystem.DTOs;
using COSGeniusSystem.Exceptions;
using COSGeniusSystem.Models;
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
            LicenseNumber = d.LicenseNumber,
            Phone = d.Phone,
            IsActive = d.IsActive,
            Username = d.User != null ? d.User.Username : null,
            Email = d.User != null ? d.User.Email : null
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

    public async Task<DoctorDetailDto> CreateDoctorAsync(CreateDoctorRequestDto request, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(request.FullName))
            throw new BadRequestException("Họ và tên bác sĩ không được để trống.");

        if (request.UserId.HasValue && request.UserId.Value > 0)
        {
            var user = await _context.Users
                .Include(u => u.UserRoles)
                    .ThenInclude(ur => ur.Role)
                .FirstOrDefaultAsync(u => u.UserId == request.UserId.Value, ct);

            if (user == null)
                throw new NotFoundException($"Không tìm thấy tài khoản người dùng có ID {request.UserId.Value}.");

            var existingDoc = await _context.Doctors.FirstOrDefaultAsync(d => d.UserId == request.UserId.Value, ct);
            if (existingDoc != null)
                throw new ConflictException($"Tài khoản '{user.Username}' đã có hồ sơ bác sĩ trên hệ thống.");

            // Gán role DOCTOR nếu user chưa có
            if (!user.UserRoles.Any(ur => ur.Role.RoleCode == RoleConstants.Doctor))
            {
                var docRole = await _context.Roles.FirstOrDefaultAsync(r => r.RoleCode == RoleConstants.Doctor, ct);
                if (docRole != null)
                {
                    user.UserRoles.Add(new UserRole
                    {
                        UserId = user.UserId,
                        RoleId = docRole.RoleId,
                        AssignedAt = DateTime.UtcNow
                    });
                }
            }
        }

        var doctor = new Doctor
        {
            UserId = request.UserId > 0 ? request.UserId : null,
            FullName = request.FullName.Trim(),
            Specialization = request.Specialization?.Trim(),
            LicenseNumber = request.LicenseNumber?.Trim(),
            Phone = request.Phone?.Trim(),
            IsActive = request.IsActive
        };

        _context.Doctors.Add(doctor);
        await _context.SaveChangesAsync(ct);

        return (await GetDoctorByIdAsync(doctor.DoctorId))!;
    }

    public async Task<DoctorDetailDto> UpdateDoctorAsync(int id, UpdateDoctorRequestDto request, CancellationToken ct = default)
    {
        var doctor = await _context.Doctors
            .Include(d => d.User)
            .FirstOrDefaultAsync(d => d.DoctorId == id, ct);

        if (doctor == null)
            throw new NotFoundException($"Không tìm thấy hồ sơ bác sĩ có ID {id}.");

        if (string.IsNullOrWhiteSpace(request.FullName))
            throw new BadRequestException("Họ và tên bác sĩ không được để trống.");

        doctor.FullName = request.FullName.Trim();
        doctor.Specialization = request.Specialization?.Trim();
        doctor.LicenseNumber = request.LicenseNumber?.Trim();
        doctor.Phone = request.Phone?.Trim();
        doctor.IsActive = request.IsActive;

        if (request.UserId.HasValue && request.UserId.Value > 0 && request.UserId != doctor.UserId)
        {
            var user = await _context.Users
                .Include(u => u.UserRoles)
                    .ThenInclude(ur => ur.Role)
                .FirstOrDefaultAsync(u => u.UserId == request.UserId.Value, ct);

            if (user == null)
                throw new NotFoundException($"Không tìm thấy tài khoản người dùng có ID {request.UserId.Value}.");

            var existingDoc = await _context.Doctors.FirstOrDefaultAsync(d => d.UserId == request.UserId.Value && d.DoctorId != id, ct);
            if (existingDoc != null)
                throw new ConflictException($"Tài khoản '{user.Username}' đã được liên kết với một hồ sơ bác sĩ khác.");

            if (!user.UserRoles.Any(ur => ur.Role.RoleCode == RoleConstants.Doctor))
            {
                var docRole = await _context.Roles.FirstOrDefaultAsync(r => r.RoleCode == RoleConstants.Doctor, ct);
                if (docRole != null)
                {
                    user.UserRoles.Add(new UserRole
                    {
                        UserId = user.UserId,
                        RoleId = docRole.RoleId,
                        AssignedAt = DateTime.UtcNow
                    });
                }
            }
            doctor.UserId = request.UserId.Value;
        }

        await _context.SaveChangesAsync(ct);
        return (await GetDoctorByIdAsync(doctor.DoctorId))!;
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
            .Where(d => !string.IsNullOrWhiteSpace(d.Specialization))
            .Select(d => d.Specialization!.Trim())
            .Distinct()
            .OrderBy(s => s)
            .ToList();

        // Lọc danh sách bác sĩ theo keyword và specialization
        var filtered = allDoctors.AsEnumerable();

        if (!string.IsNullOrWhiteSpace(query.Specialization) && query.Specialization.Trim() != "all" && query.Specialization.Trim() != "Tất cả chuyên khoa")
        {
            var specTarget = query.Specialization.Trim().ToLowerInvariant();
            filtered = filtered.Where(d => d.Specialization != null && d.Specialization.ToLowerInvariant().Contains(specTarget));
        }

        if (!string.IsNullOrWhiteSpace(query.Keyword))
        {
            var kw = query.Keyword.Trim().ToLowerInvariant();
            filtered = filtered.Where(d =>
                d.FullName.ToLowerInvariant().Contains(kw) ||
                (d.Phone != null && d.Phone.ToLowerInvariant().Contains(kw)) ||
                (d.LicenseNumber != null && d.LicenseNumber.ToLowerInvariant().Contains(kw)));
        }

        var doctorList = filtered.OrderBy(d => d.DoctorId).ToList();

        // Lấy danh sách lượt khám trong ngày để tính hàng đợi & ca đang khám
        var todayRegistrations = await _context.ExaminationRegistrations.AsNoTracking()
            .Include(r => r.Patient)
            .Where(r => r.BusinessDate == today)
            .ToListAsync(ct);

        var resultItems = new List<DoctorClinicItemDto>();
        var todayTreatments = await _context.TreatmentSessions.AsNoTracking()
            .Where(s => s.ScheduledDate == today && (s.Status == "REQUESTED" || s.Status == "Chờ điều trị"
                || s.Status == "IN_PROGRESS"))
            .Select(s => new
            {
                s.AssignedDoctorId, s.PerformedBy, s.Status, s.SessionId,
                s.Plan.Record.Patient.FullName, s.Plan.Record.Patient.PatientCode,
                s.Plan.Record.Patient.PatientId
            }).ToListAsync(ct);

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
            else
            {
                currentPatientText = "0";
            }

            var item = new DoctorClinicItemDto
            {
                DoctorId = d.DoctorId,
                FullName = d.FullName,
                Specialization = d.Specialization,
                LicenseNumber = d.LicenseNumber,
                Phone = d.Phone,
                Room = string.Empty,
                IsActive = d.IsActive,
                StatusText = d.IsActive ? "Hoạt động" : "Ngừng hoạt động",
                AbsenceReason = null,
                WaitingCount = doctorWaitingCount,
                CurrentExaminingPatient = currentPatientText,
                WaitingTreatmentCount = todayTreatments.Count(s => s.AssignedDoctorId == d.DoctorId
                    && (s.Status == "REQUESTED" || s.Status == "Chờ điều trị")),
                CurrentTreatingPatients = todayTreatments
                    .Where(s => (s.PerformedBy ?? s.AssignedDoctorId) == d.DoctorId && s.Status == "IN_PROGRESS")
                    .OrderBy(s => s.SessionId)
                    .Select(s => $"{s.FullName} ({s.PatientCode ?? $"BN{s.PatientId:D5}"})")
                    .ToList()
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
}

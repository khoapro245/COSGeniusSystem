using System.Data;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using COSGeniusSystem.Commons;
using COSGeniusSystem.DTOs;
using COSGeniusSystem.Exceptions;
using COSGeniusSystem.Models;
using Microsoft.EntityFrameworkCore;

namespace COSGeniusSystem.Services;

public sealed partial class TreatmentWorkflowService
{
    private static string AdmissionFailure(string message) => message switch
    {
        "The treatment plan is not active or has ended." => "Liệu trình không còn hiệu lực.",
        "The data has changed. Please reload before trying again." => "Dữ liệu đã thay đổi. Vui lòng tải lại liệu trình trước khi đăng ký.",
        "All indicated services must be fully paid before treatment." => "Liệu trình chưa được thanh toán đủ các dịch vụ đã chỉ định.",
        _ => message
    };
    private sealed class AdmissionListRow
    {
        public Guid? AdmissionId { get; set; }
        public int? LegacySessionId { get; set; }
        public int PatientId { get; set; }
        public string PatientName { get; set; } = "";
        public string? PatientCode { get; set; }
        public string? Phone { get; set; }
        public int? DoctorId { get; set; }
        public string? DoctorName { get; set; }
        public DateOnly? ScheduledDate { get; set; }
        public string? Shift { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? AcceptedAt { get; set; }
        public int RequestCount { get; set; }
        public int CompletedCount { get; set; }
        public string Status { get; set; } = "";
        public byte[] RowVersion { get; set; } = [];
    }
    public static string AdmissionStatus(IEnumerable<string> statuses, bool accepted)
    {
        var values = statuses.ToArray();
        if (values.Length > 0 && values.All(s => s == "COMPLETED")) return "COMPLETED";
        if (values.Length > 0 && values.All(s => s is "COMPLETED" or "CANCELLED" or "EXPIRED")) return "ENDED_INCOMPLETE";
        return values.Contains("IN_PROGRESS") ? "IN_PROGRESS" : accepted ? "ACCEPTED" : "WAITING";
    }

    public async Task<IReadOnlyList<AdmissionOverview>> CreateAdmissionsAsync(ClaimsPrincipal actor, CreateTreatmentAdmission request, CancellationToken ct)
    {
        Reception(actor); Validate(request);
        foreach (var item in request.Items) Validate(item);
        if (request.RegistrationKey == Guid.Empty) throw new BadRequestException("Thiếu mã lần đăng ký.");
        if (request.Items.Select(i => i.PlanId).Distinct().Count() != request.Items.Count)
            throw new BadRequestException("Không chọn trùng liệu trình trong cùng lần đăng ký.");
        // Canonical payload binds retries to this exact registration, regardless of item ordering.
        var fingerprint = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(new
        {
            request.PatientId, request.ScheduledDate, request.Shift, Note = request.Note?.Trim(),
            Items = request.Items.OrderBy(i => i.PlanId).Select(i => new { i.PlanId, i.AssignedDoctorId, i.RowVersion })
        }))));
        await AppointmentExpiry.RefreshAsync(db, ct);
        await using var tx = db.Database.IsRelational() ? await db.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted, ct) : null;
        if (db.Database.IsRelational())
        {
            var resource = $"TreatmentRegistration:{request.RegistrationKey}";
            await db.Database.ExecuteSqlInterpolatedAsync($@"DECLARE @result int;
                EXEC @result = sys.sp_getapplock @Resource={resource}, @LockMode='Exclusive', @LockOwner='Transaction', @LockTimeout=15000;
                IF @result < 0 THROW 51035, 'Cannot lock treatment registration.', 1;", ct);
        }
        var previous = await db.TreatmentAdmissions.Where(g => g.RegistrationKey == request.RegistrationKey).ToListAsync(ct);
        if (previous.Count > 0)
        {
            if (previous.Any(g => g.CreatedBy != UserId(actor) || g.RequestFingerprint != fingerprint))
                throw new ConflictException("Mã lần đăng ký đã được sử dụng với nội dung khác. Hãy tải lại dữ liệu.");
            var replay = new List<AdmissionOverview>();
            foreach (var group in previous) replay.Add(await AdmissionAsync(actor, group.AdmissionId, ct));
            if (tx != null) await tx.CommitAsync(ct);
            return replay;
        }
        if (request.ScheduledDate < DateOnly.FromDateTime(DateTime.UtcNow.AddHours(7)))
            throw new BadRequestException("Ngày điều trị không được trong quá khứ.");
        AppointmentTime.ValidateMorning(request.ScheduledDate!.Value, request.Shift);
        // Lock all plans in stable order, validate every item before writing any group/session.
        foreach (var item in request.Items.OrderBy(i => i.PlanId))
        {
            var plan = db.Database.IsRelational()
                ? await db.TreatmentPlans.FromSqlInterpolated($"SELECT * FROM dbo.TreatmentPlan WITH (UPDLOCK, ROWLOCK) WHERE PlanID = {item.PlanId}").SingleOrDefaultAsync(ct)
                : await db.TreatmentPlans.SingleOrDefaultAsync(p => p.PlanId == item.PlanId, ct);
            if (plan == null) throw new NotFoundException($"Không tìm thấy liệu trình {item.PlanId}.");
            try
            {
                if (!await db.MedicalRecords.AnyAsync(r => r.RecordId == plan.RecordId && r.PatientId == request.PatientId, ct))
                    throw new BadRequestException("Liệu trình không thuộc bệnh nhân đã chọn.");
                Effective(plan); Version(plan.RowVersion, item.RowVersion);
                if (await PatientRequestGuard.HasActiveExaminationAsync(db, request.PatientId, ct))
                    throw new ConflictException("Bệnh nhân đang có yêu cầu khám chưa kết thúc.");
                if (await db.TreatmentSessions.AnyAsync(s => s.PlanId == item.PlanId && (s.Status == "REQUESTED" || s.Status == "IN_PROGRESS"), ct))
                    throw new ConflictException("Liệu trình đã có yêu cầu điều trị đang hiệu lực.");
                if (await db.TreatmentSessions.CountAsync(s => s.PlanId == item.PlanId && s.Status == "COMPLETED", ct) >= plan.PlannedSessionCount)
                    throw new ConflictException("Liệu trình không còn buổi điều trị.");
                if (!await db.TreatmentPlanDetails.AnyAsync(d => d.PlanId == item.PlanId && d.PlannedQuantity > (d.UsageHistories.Where(u => u.Session.Status == "COMPLETED").Sum(u => (int?)u.Quantity) ?? 0), ct))
                    throw new ConflictException("Liệu trình không còn dịch vụ cần thực hiện.");
                await PaymentAsync(item.PlanId, ct);
                if (!await db.Doctors.AnyAsync(d => d.DoctorId == item.AssignedDoctorId && d.IsActive && d.User != null && d.User.IsActive
                    && d.User.UserRoles.Any(r => r.Role.RoleCode == RoleConstants.Doctor), ct))
                    throw new BadRequestException("Bác sĩ được chọn không hoạt động.");
                var planned = await db.TreatmentSessions.Where(s => s.PlanId == item.PlanId && s.Status == "PLANNED").OrderBy(s => s.SessionNumber).FirstOrDefaultAsync(ct);
                if (planned != null && (planned.StartedAt != null || planned.CompletedAt != null || planned.PerformedBy != null || await db.UsageHistories.AnyAsync(u => u.SessionId == planned.SessionId, ct)))
                    throw new ConflictException("Buổi dự kiến đã có dữ liệu thực hiện, cần kiểm tra lại.");
            }
            catch (ConflictException ex) { throw new ConflictException($"Liệu trình {plan.PlanName} ({item.PlanId}): {AdmissionFailure(ex.Message)}"); }
            catch (BadRequestException ex) { throw new BadRequestException($"Liệu trình {plan.PlanName} ({item.PlanId}): {ex.Message}"); }
        }
        var ids = new List<Guid>();
        foreach (var doctorItems in request.Items.GroupBy(i => i.AssignedDoctorId))
        {
            var group = new TreatmentAdmission
            {
                AdmissionId = Guid.NewGuid(), RegistrationKey = request.RegistrationKey, RequestFingerprint = fingerprint,
                PatientId = request.PatientId, DoctorId = doctorItems.Key, ScheduledDate = request.ScheduledDate.Value,
                Shift = request.Shift, CreatedAt = DateTime.UtcNow, CreatedBy = UserId(actor)
            };
            db.TreatmentAdmissions.Add(group);
            foreach (var item in doctorItems)
            {
                var session = await RequestAsync(actor, item.PlanId, new RequestTreatment
                {
                    AssignedDoctorId = item.AssignedDoctorId, ScheduledDate = request.ScheduledDate,
                    Shift = request.Shift, Note = request.Note, RowVersion = item.RowVersion
                }, ct);
                var entity = await db.TreatmentSessions.SingleAsync(s => s.SessionId == session.Session.SessionId, ct);
                entity.Admission = group;
            }
            ids.Add(group.AdmissionId);
        }
        await db.SaveChangesAsync(ct);
        if (tx != null) await tx.CommitAsync(ct);
        var result = new List<AdmissionOverview>();
        foreach (var id in ids) result.Add(await AdmissionAsync(actor, id, ct));
        return result;
    }

    public async Task<AdmissionOverview> AdmissionAsync(ClaimsPrincipal actor, Guid id, CancellationToken ct)
    {
        await AppointmentExpiry.RefreshAsync(db, ct);
        var doctor = await ReaderAsync(actor, ct);
        var g = await db.TreatmentAdmissions.AsNoTracking().Include(g => g.Patient).Include(g => g.Doctor)
            .SingleOrDefaultAsync(g => g.AdmissionId == id, ct) ?? throw new NotFoundException("Không tìm thấy nhóm tiếp nhận.");
        if (doctor.HasValue && doctor != g.DoctorId) throw new ForbiddenException();
        var sessions = await db.TreatmentSessions.AsNoTracking().Where(s => s.AdmissionId == id).OrderBy(s => s.SessionId).Select(SessionProjection).ToListAsync(ct);
        return new(new(g.AdmissionId, null, g.PatientId, g.Patient.FullName, g.Patient.PatientCode, g.Patient.Phone,
            g.DoctorId, g.Doctor.FullName, g.ScheduledDate, g.Shift, g.CreatedAt, g.AcceptedAt, sessions.Count,
            sessions.Count(s => s.Status == "COMPLETED"), AdmissionStatus(sessions.Select(s => s.Status), g.AcceptedAt.HasValue), g.RowVersion), sessions);
    }

    public async Task<PagedResult<AdmissionSummary>> AdmissionsAsync(ClaimsPrincipal actor, SessionQuery query, CancellationToken ct)
    {
        await AppointmentExpiry.RefreshAsync(db, ct);
        Validate(query);
        if (query.FromDate > query.ToDate) throw new BadRequestException("Khoảng ngày không hợp lệ.");
        var doctor = await ReaderAsync(actor, ct);
        var rows = AdmissionRows(doctor);
        if (query.DoctorId.HasValue) rows = rows.Where(r => r.DoctorId == query.DoctorId);
        if (query.FromDate.HasValue) rows = rows.Where(r => r.ScheduledDate >= query.FromDate);
        if (query.ToDate.HasValue) rows = rows.Where(r => r.ScheduledDate <= query.ToDate);
        if (!string.IsNullOrWhiteSpace(query.Status)) rows = rows.Where(r => r.Status == query.Status);
        var keyword = (query.Keyword ?? query.Search ?? query.PatientKeyword)?.Trim();
        if (!string.IsNullOrEmpty(keyword)) rows = rows.Where(r => r.PatientName.Contains(keyword) || (r.PatientCode != null && r.PatientCode.Contains(keyword)) || (r.Phone != null && r.Phone.Contains(keyword)));
        var count = await rows.CountAsync(ct);
        var page = await rows.OrderByDescending(r => r.ScheduledDate).ThenByDescending(r => r.CreatedAt).ThenBy(r => r.AdmissionId).ThenBy(r => r.LegacySessionId)
            .Skip((query.PageNumber - 1) * 10).Take(10).ToListAsync(ct);
        return new(page.Select(r => new AdmissionSummary(r.AdmissionId, r.LegacySessionId, r.PatientId, r.PatientName, r.PatientCode, r.Phone,
            r.DoctorId, r.DoctorName, r.ScheduledDate, r.Shift, r.CreatedAt, r.AcceptedAt, r.RequestCount, r.CompletedCount, r.Status, r.RowVersion)).ToList(), query.PageNumber, 10, count);
    }

    private IQueryable<AdmissionListRow> AdmissionRows(int? doctor)
    {
        var groups = db.TreatmentAdmissions.AsNoTracking().Where(g => !doctor.HasValue || g.DoctorId == doctor);
        var legacy = db.TreatmentSessions.AsNoTracking().Where(s => s.AdmissionId == null && s.Status != "PLANNED"
            && (!doctor.HasValue || s.AssignedDoctorId == doctor || s.PerformedBy == doctor));
        // Relational projection keeps union, filtering, count and paging on the server.
        var rows = groups.Select(g => new AdmissionListRow
        {
            AdmissionId = (Guid?)g.AdmissionId, LegacySessionId = (int?)null, PatientId = g.PatientId,
            PatientName = g.Patient.FullName, PatientCode = g.Patient.PatientCode, Phone = g.Patient.Phone,
            DoctorId = (int?)g.DoctorId, DoctorName = (string?)g.Doctor.FullName,
            ScheduledDate = (DateOnly?)g.ScheduledDate, Shift = (string?)g.Shift, CreatedAt = g.CreatedAt, AcceptedAt = g.AcceptedAt,
            RequestCount = g.Sessions.Count, CompletedCount = g.Sessions.Count(s => s.Status == "COMPLETED"),
            Status = g.Sessions.Any() && g.Sessions.All(s => s.Status == "COMPLETED") ? "COMPLETED"
                : g.Sessions.Any() && g.Sessions.All(s => s.Status == "COMPLETED" || s.Status == "CANCELLED" || s.Status == "EXPIRED") ? "ENDED_INCOMPLETE"
                : g.Sessions.Any(s => s.Status == "IN_PROGRESS") ? "IN_PROGRESS" : g.AcceptedAt != null ? "ACCEPTED" : "WAITING", RowVersion = g.RowVersion
        }).Concat(legacy.Select(s => new AdmissionListRow
        {
            AdmissionId = (Guid?)null, LegacySessionId = (int?)s.SessionId, PatientId = s.Plan.Record.PatientId,
            PatientName = s.Plan.Record.Patient.FullName, PatientCode = s.Plan.Record.Patient.PatientCode, Phone = s.Plan.Record.Patient.Phone,
            DoctorId = s.AssignedDoctorId, DoctorName = s.AssignedDoctor == null ? null : s.AssignedDoctor.FullName,
            ScheduledDate = s.ScheduledDate, Shift = s.Shift, CreatedAt = s.RequestedAt ?? s.CreatedAt, AcceptedAt = s.StartedAt,
            RequestCount = 1, CompletedCount = s.Status == "COMPLETED" ? 1 : 0,
            Status = s.Status == "REQUESTED" ? "WAITING" : s.Status == "CANCELLED" || s.Status == "EXPIRED" ? "ENDED_INCOMPLETE" : s.Status, RowVersion = s.RowVersion
        }));
        return rows;
    }

    public async Task<AdmissionOverview> UpdateAdmissionAsync(ClaimsPrincipal actor, Guid id, VersionRequest request, bool close, CancellationToken ct)
    {
        Validate(request);
        await AppointmentExpiry.RefreshAsync(db, ct);
        var doctor = await DoctorAsync(actor, ct);
        await using var tx = db.Database.IsRelational() ? await db.Database.BeginTransactionAsync(ct) : null;
        var g = db.Database.IsRelational()
            ? await db.TreatmentAdmissions.FromSqlInterpolated($"SELECT * FROM dbo.TreatmentAdmission WITH (UPDLOCK, ROWLOCK) WHERE AdmissionId = {id}").SingleOrDefaultAsync(ct)
            : await db.TreatmentAdmissions.SingleOrDefaultAsync(g => g.AdmissionId == id, ct);
        if (g == null) throw new NotFoundException("Không tìm thấy nhóm tiếp nhận.");
        if (g.DoctorId != doctor) throw new ForbiddenException();
        var statuses = await db.TreatmentSessions.Where(s => s.AdmissionId == id).Select(s => s.Status).ToListAsync(ct);
        var status = AdmissionStatus(statuses, g.AcceptedAt.HasValue);
        if (close)
        {
            if (status is not ("COMPLETED" or "ENDED_INCOMPLETE")) throw new ConflictException("Nhóm còn yêu cầu chưa thực hiện. Vui lòng xử lý hoặc hủy từng yêu cầu.");
            if (g.ClosedAt == null) { Version(g.RowVersion, request.RowVersion); g.ClosedAt = DateTime.UtcNow; g.ClosedBy = UserId(actor); }
        }
        else if (g.AcceptedAt == null)
        {
            Version(g.RowVersion, request.RowVersion);
            if (g.ScheduledDate != DateOnly.FromDateTime(DateTime.UtcNow.AddHours(7)) || status is "COMPLETED" or "ENDED_INCOMPLETE")
                throw new ConflictException("Chỉ tiếp nhận nhóm còn yêu cầu trong ngày điều trị.");
            g.AcceptedAt = DateTime.UtcNow; g.AcceptedBy = UserId(actor);
        }
        await db.SaveChangesAsync(ct);
        if (tx != null) await tx.CommitAsync(ct);
        return await AdmissionAsync(actor, id, ct);
    }
}

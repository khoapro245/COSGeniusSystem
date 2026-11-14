using COSGeniusSystem.Services;
using System.ComponentModel.DataAnnotations;
using System.Data;
using System.Linq.Expressions;
using System.Security.Claims;
using COSGeniusSystem.Commons;
using COSGeniusSystem.Data;
using COSGeniusSystem.DTOs;
using COSGeniusSystem.Exceptions;
using COSGeniusSystem.Models;
using Microsoft.EntityFrameworkCore;

namespace COSGeniusSystem.Services;

public sealed class TreatmentWorkflowService(ApplicationDbContext db, IPatientService? patients = null,
    IConfiguration? configuration = null) : ITreatmentWorkflowService
{
    // COURSE requires full prepayment; SINGLE is billed after completed usage.

    private static int UserId(ClaimsPrincipal actor) =>
        int.TryParse(actor.FindFirstValue(ClaimTypes.NameIdentifier), out var id) && id > 0 ? id : throw new UnauthorizedException();
    private static void Validate(object? value)
    {
        if (value == null) throw new BadRequestException("Request and list items cannot be null.");
        var errors = new List<ValidationResult>();
        if (!Validator.TryValidateObject(value, new ValidationContext(value), errors, true))
            throw new BadRequestException(string.Join(" ", errors.Select(e => e.ErrorMessage)));
    }
    private static void Version(byte[] actual, byte[]? expected)
    {
        if (expected is null || expected.Length == 0) return;
        if (expected.Length == 8 && !actual.SequenceEqual(expected))
            throw new ConflictException("Data changed. Reload and retry with the latest RowVersion.");
    }
    private static void Reception(ClaimsPrincipal actor)
    {
        _ = UserId(actor);
        if (!actor.IsInRole(RoleConstants.ReceptionStaff)) throw new ForbiddenException();
    }
    private async Task<int> DoctorAsync(ClaimsPrincipal actor, CancellationToken ct)
    {
        if (!actor.IsInRole(RoleConstants.Doctor)) throw new ForbiddenException();
        var userId = UserId(actor);
        return await db.Doctors.Where(d => d.UserId == userId && d.IsActive && d.User != null && d.User.IsActive
            && d.User.UserRoles.Any(r => r.Role.RoleCode == RoleConstants.Doctor))
            .Select(d => (int?)d.DoctorId).SingleOrDefaultAsync(ct) ?? throw new ForbiddenException("No active doctor account.");
    }
    private async Task<int?> ReaderAsync(ClaimsPrincipal actor, CancellationToken ct)
    {
        _ = UserId(actor);
        return actor.IsInRole(RoleConstants.ReceptionStaff) ? null : await DoctorAsync(actor, ct);
    }
    private async Task AccessPlanAsync(ClaimsPrincipal actor, TreatmentPlan plan, CancellationToken ct)
    {
        _ = UserId(actor);
        await Task.CompletedTask;
    }
    private async Task AssignedAsync(ClaimsPrincipal actor, TreatmentSession session, CancellationToken ct)
    {
        if (actor.IsInRole(RoleConstants.Admin)) return;
        if (!actor.IsInRole(RoleConstants.Doctor)) throw new ForbiddenException();
        var docId = await DoctorAsync(actor, ct);
        if (session.AssignedDoctorId == null)
        {
            session.AssignedDoctorId = docId;
        }
    }
    private static void Effective(TreatmentPlan plan)
    {
        if (plan.Status is not ("CONFIRMED" or "IN_PROGRESS")) throw new ConflictException("The treatment plan is not effective.");
    }
    private static readonly Expression<Func<TreatmentPlan, PlanSummary>> PlanProjection = p => new(
        p.PlanId, p.PlanCode, p.PlanName, p.RecordId,
        p.Record != null ? p.Record.PatientId : 0,
        p.Record != null && p.Record.Patient != null ? p.Record.Patient.PatientCode : null,
        p.Record != null && p.Record.Patient != null ? p.Record.Patient.FullName : "Không xác định",
        p.Record != null && p.Record.Patient != null ? p.Record.Patient.Phone : null,
        p.DoctorId,
        p.Doctor != null ? p.Doctor.FullName : "Chưa phân công",
        p.Status, p.CreatedAt,
        p.PlannedSessionCount, p.TreatmentSessions.Count(s => s.Status == "COMPLETED"),
        p.TreatmentSessions.Count(s => s.Status == "REQUESTED" || s.Status == "IN_PROGRESS"), p.RowVersion);
    private static readonly Expression<Func<TreatmentSession, SessionSummary>> SessionProjection = s => new(
        s.SessionId, s.PlanId,
        s.Plan != null ? s.Plan.PlanCode : null,
        s.Plan != null ? s.Plan.PlanName : "",
        s.SessionNumber,
        s.Plan != null && s.Plan.Record != null ? s.Plan.Record.PatientId : 0,
        s.Plan != null && s.Plan.Record != null && s.Plan.Record.Patient != null ? s.Plan.Record.Patient.PatientCode : null,
        s.Plan != null && s.Plan.Record != null && s.Plan.Record.Patient != null ? s.Plan.Record.Patient.FullName : "Không xác định",
        s.Plan != null && s.Plan.Record != null && s.Plan.Record.Patient != null ? s.Plan.Record.Patient.Phone : null,
        s.AssignedDoctorId,
        s.AssignedDoctor != null ? s.AssignedDoctor.FullName : "Chưa phân công",
        s.PerformedBy,
        s.PerformedByNavigation != null ? s.PerformedByNavigation.FullName : null,
        s.RequestedBy,
        s.RequestedAt, s.ScheduledDate, s.StartedAt, s.CompletedAt, s.Status, s.ResultNote, s.RowVersion, s.Shift, s.RequestCode, s.QueueNumber);

    public async Task<PagedResult<PlanSummary>> PlansAsync(ClaimsPrincipal actor, PlanQuery query, CancellationToken ct)
    {
        Validate(query);
        if (query.FromDate > query.ToDate) throw new BadRequestException("Invalid date range.");
        _ = UserId(actor);
        var q = db.TreatmentPlans.AsNoTracking();
        if (query.PatientId.HasValue) q = q.Where(p => p.Record.PatientId == query.PatientId);
        if (query.DoctorId.HasValue) q = q.Where(p => p.DoctorId == query.DoctorId);
        if (query.Status != null) q = q.Where(p => p.Status == query.Status);
        if (query.FromDate.HasValue) { var start = query.FromDate.Value == DateOnly.MinValue ? DateTime.MinValue : query.FromDate.Value.ToDateTime(TimeOnly.MinValue).AddHours(-7); q = q.Where(p => p.CreatedAt >= start); }
        if (query.ToDate.HasValue && query.ToDate.Value != DateOnly.MaxValue) { var end = query.ToDate.Value.ToDateTime(TimeOnly.MinValue).AddDays(1).AddHours(-7); q = q.Where(p => p.CreatedAt < end); }
        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var search = query.Search.Trim();
            q = q.Where(p => (p.PlanCode != null && p.PlanCode.Contains(search)) || p.PlanName.Contains(search)
                || p.Record.Patient.FullName.Contains(search) || (p.Record.Patient.PatientCode != null && p.Record.Patient.PatientCode.Contains(search))
                || (p.Record.Patient.Phone != null && p.Record.Patient.Phone.Contains(search)));
        }
        var count = await q.CountAsync(ct);
        var items = await q.OrderByDescending(p => p.CreatedAt).ThenByDescending(p => p.PlanId)
            .Skip((query.PageNumber - 1) * query.PageSize).Take(query.PageSize).Select(PlanProjection).ToListAsync(ct);
        return new(items, query.PageNumber, query.PageSize, count);
    }

    private async Task<bool> FullyPaidAsync(int planId, CancellationToken ct)
    {
        // A paid invoice for medicines alone does not settle any treatment service.
        // Quantity coverage uses only paid, same-record service lines at the prescribed snapshot price.
        return await db.TreatmentPlanDetails.AnyAsync(d => d.PlanId == planId, ct)
            && !await db.TreatmentPlanDetails.Where(d => d.PlanId == planId).AnyAsync(d =>
            (db.InvoiceDetails.Where(i => i.PlanDetailId == d.PlanDetailId && i.Invoice.RecordId == d.Plan.RecordId
                && (i.Invoice.PaymentStatus == "PAID" || i.Invoice.PaymentStatus == "Đã thanh toán") && i.ItemType == "SERVICE" && i.UnitPrice == d.UnitPrice)
                .Sum(i => (decimal?)i.Quantity) ?? 0) < d.PlannedQuantity, ct);
    }
    private async Task PaymentAsync(int planId, CancellationToken ct)
    {
        _ = configuration; // Legacy setting cannot disable mandatory COURSE prepayment.
        if (await db.TreatmentPlans.AnyAsync(p => p.PlanId == planId && p.TreatmentType == "COURSE", ct) && !await FullyPaidAsync(planId, ct))
            throw new ConflictException("All prescribed treatment services must be fully paid before treatment.");
    }
    public async Task<PlanOverview> PlanAsync(ClaimsPrincipal actor, int id, CancellationToken ct)
    {
        var plan = await db.TreatmentPlans.AsNoTracking().SingleOrDefaultAsync(p => p.PlanId == id, ct)
            ?? throw new NotFoundException("Treatment plan not found.");
        await AccessPlanAsync(actor, plan, ct);
        var summary = await db.TreatmentPlans.AsNoTracking().Where(p => p.PlanId == id).Select(PlanProjection).SingleAsync(ct);
        var record = await db.MedicalRecords.AsNoTracking().Where(r => r.RecordId == plan.RecordId)
            .Select(r => new { r.PatientId, r.RegistrationId, r.ExamDate, r.FinalDiagnosis, r.AllergyNoteSnapshot }).SingleAsync(ct);
        var services = await db.TreatmentPlanDetails.AsNoTracking().Where(d => d.PlanId == id).OrderBy(d => d.PlanDetailId)
            .Select(d => new ServiceProgress(d.PlanDetailId, d.ServiceId, d.ServiceNameSnapshot, d.UnitSnapshot, d.UnitPrice,
                d.PlannedQuantity, d.UsageHistories.Where(u => u.Session.Status == "COMPLETED").Sum(u => u.Quantity),
                d.PlannedQuantity - d.UsageHistories.Where(u => u.Session.Status == "COMPLETED").Sum(u => u.Quantity), d.Instructions)).ToListAsync(ct);
        var paid = services.Count > 0 && await FullyPaidAsync(id, ct);
        var effective = plan.Status is "CONFIRMED" or "IN_PROGRESS";
        var hasInvoices = await db.InvoiceDetails.AnyAsync(i => i.PlanDetail != null && i.PlanDetail.PlanId == id, ct);
        var doctor = actor.IsInRole(RoleConstants.Doctor) ? await DoctorAsync(actor, ct) : (int?)null;
        var patientResp = patients != null ? await patients.GetByIdAsync(record.PatientId, ct) : new PatientResponse { PatientId = record.PatientId, FullName = summary.PatientName, Phone = summary.Phone };
        return new(summary, patientResp, record.RegistrationId,
            record.ExamDate, record.FinalDiagnosis, record.AllergyNoteSnapshot, plan.Description, plan.TreatmentType,
            plan.StartDate, plan.EndDate, Math.Max(0, plan.PlannedSessionCount - summary.CompletedSessions), paid, plan.TreatmentType == "COURSE",
            actor.IsInRole(RoleConstants.ReceptionStaff) && effective && summary.ActiveRequests == 0
                && summary.CompletedSessions < plan.PlannedSessionCount && services.Any(s => s.RemainingQuantity > 0)
                && (plan.TreatmentType != "COURSE" || paid),
            doctor == plan.DoctorId && (effective || plan.Status == "DRAFT") && summary.ActiveRequests == 0
                && summary.CompletedSessions == 0 && !hasInvoices
                && !await db.UsageHistories.AnyAsync(u => u.Session.PlanId == id, ct), services, plan.SessionDetailsJson);
    }

    public async Task<PagedResult<SessionSummary>> SessionsAsync(ClaimsPrincipal actor, SessionQuery query, bool mine, CancellationToken ct)
    {
        Validate(query);
        if (query.FromDate > query.ToDate) throw new BadRequestException("Invalid date range.");
        var doctor = mine ? await DoctorAsync(actor, ct) : (int?)null;
        var q = db.TreatmentSessions.AsNoTracking();
        if (query.ExcludePlanned) q = q.Where(s => s.Status != "PLANNED");
        if (mine && doctor.HasValue)
        {
            q = q.Where(s => s.AssignedDoctorId == doctor || s.PerformedBy == doctor);
        }
        if (query.PlanId.HasValue) q = q.Where(s => s.PlanId == query.PlanId);
        if (query.DoctorId.HasValue) q = q.Where(s => s.AssignedDoctorId == query.DoctorId);
        var st = query.Status?.Trim().ToUpperInvariant();
        if (!string.IsNullOrWhiteSpace(st) && st is not ("ALL" or "TẤT CẢ" or "TAT CA"))
        {
            string norm = st switch
            {
                "WAITING" or "REQUESTED" or "CHỜ ĐIỀU TRỊ" or "CHO DIEU TRI" or "CHỜ TIẾP NHẬN" => "REQUESTED",
                "IN_PROGRESS" or "INPROGRESS" or "ĐANG ĐIỀU TRỊ" or "DANG DIEU TRI" => "IN_PROGRESS",
                "COMPLETED" or "HOÀN TẤT" or "HOÀN THÀNH" or "ĐÃ HOÀN TẤT" => "COMPLETED",
                "CANCELLED" or "CANCELED" or "ĐÃ HỦY" or "HỦY" => "CANCELLED",
                "PLANNED" => "PLANNED",
                _ => query.Status.Trim()
            };
            q = q.Where(s => s.Status == norm);
        }

        if (query.FromDate.HasValue) q = q.Where(s => s.ScheduledDate >= query.FromDate.Value);
        if (query.ToDate.HasValue) q = q.Where(s => s.ScheduledDate <= query.ToDate.Value);

        if (!string.IsNullOrWhiteSpace(query.Phone))
        {
            var p = query.Phone.Trim();
            q = q.Where(s => s.Plan.Record.Patient.Phone != null && s.Plan.Record.Patient.Phone.Contains(p));
        }

        var vc = query.VisitCode ?? query.Code;
        if (!string.IsNullOrWhiteSpace(vc))
        {
            var c = vc.Trim();
            q = q.Where(s => (s.RequestCode != null && s.RequestCode.Contains(c)) || (s.Plan.PlanCode != null && s.Plan.PlanCode.Contains(c))
                || (s.Plan.Record.Patient.PatientCode != null && s.Plan.Record.Patient.PatientCode.Contains(c)));
        }

        if (!string.IsNullOrWhiteSpace(query.PatientName))
        {
            var pn = query.PatientName.Trim();
            q = q.Where(s => s.Plan.Record.Patient.FullName.Contains(pn));
        }

        if (!string.IsNullOrWhiteSpace(query.PatientCode))
        {
            var pc = query.PatientCode.Trim();
            q = q.Where(s => s.Plan.Record.Patient.PatientCode != null && s.Plan.Record.Patient.PatientCode.Contains(pc));
        }

        if (!string.IsNullOrWhiteSpace(query.PatientKeyword))
        {
            var pk = query.PatientKeyword.Trim();
            q = q.Where(s => s.Plan.Record.Patient.FullName.Contains(pk)
                || (s.Plan.Record.Patient.PatientCode != null && s.Plan.Record.Patient.PatientCode.Contains(pk)));
        }

        var kw = query.Keyword ?? query.Search;
        if (!string.IsNullOrWhiteSpace(kw))
        {
            var search = kw.Trim();
            q = q.Where(s => s.Plan.Record.Patient.FullName.Contains(search)
                || (s.Plan.Record.Patient.PatientCode != null && s.Plan.Record.Patient.PatientCode.Contains(search))
                || (s.Plan.Record.Patient.Phone != null && s.Plan.Record.Patient.Phone.Contains(search))
                || (s.RequestCode != null && s.RequestCode.Contains(search)) || (s.Plan.PlanCode != null && s.Plan.PlanCode.Contains(search)));
        }

        var count = await q.CountAsync(ct);
        var items = await q.OrderByDescending(s => s.ScheduledDate)
            .ThenByDescending(s => s.RequestedAt ?? s.CreatedAt)
            .ThenByDescending(s => s.SessionId)
            .Skip((query.PageNumber - 1) * query.PageSize).Take(query.PageSize).Select(SessionProjection).ToListAsync(ct);
        return new(items, query.PageNumber, query.PageSize, count);
    }
    public async Task<SessionOverview> SessionAsync(ClaimsPrincipal actor, int id, CancellationToken ct)
    {
        var session = await db.TreatmentSessions.AsNoTracking().Where(s => s.SessionId == id).Select(SessionProjection).SingleOrDefaultAsync(ct)
            ?? throw new NotFoundException("Treatment session not found.");
        var plan = await PlanAsync(actor, session.PlanId, ct);
        var usage = await db.UsageHistories.AsNoTracking().Where(u => u.SessionId == id).OrderBy(u => u.UsageId)
            .Select(u => new UsageResponse(u.PlanDetailId, u.PlanDetail.ServiceNameSnapshot, u.Quantity, u.PerformedBy, u.ExecutionTime, u.ResultNote)).ToListAsync(ct);
        var assigned = actor.IsInRole(RoleConstants.Doctor) && session.AssignedDoctorId == await DoctorAsync(actor, ct);
        return new(session, plan, usage, assigned && session.Status == "REQUESTED" && (!plan.RequiresFullPayment || plan.FullyPaid),
            assigned && session.Status == "IN_PROGRESS", session.Status == "REQUESTED" && (assigned || actor.IsInRole(RoleConstants.ReceptionStaff)));
    }

    // All writers acquire locks in this order: plan -> session.
    private async Task<T> WriteAsync<T>(int planId, Func<TreatmentPlan, Task<T>> action, CancellationToken ct, int? startingDoctorId = null)
    {
        await using var tx = db.Database.IsRelational() ? await db.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted, ct) : null;
        if (startingDoctorId.HasValue) await DoctorAvailability.LockAsync(db, startingDoctorId.Value, ct);
        var plan = db.Database.IsRelational()
            ? (await db.TreatmentPlans.FromSqlInterpolated($"SELECT * FROM dbo.TreatmentPlan WITH (UPDLOCK, ROWLOCK) WHERE PlanID = {planId}")
                .SingleOrDefaultAsync(ct) ?? throw new NotFoundException("Treatment plan not found."))
            : (await db.TreatmentPlans.SingleOrDefaultAsync(p => p.PlanId == planId, ct) ?? throw new NotFoundException("Treatment plan not found."));
        var result = await action(plan);
        await db.SaveChangesAsync(ct);
        if (tx != null) await tx.CommitAsync(ct);
        return result;
    }
    private async Task<int> SessionPlanAsync(int id, CancellationToken ct) =>
        await db.TreatmentSessions.AsNoTracking().Where(s => s.SessionId == id).Select(s => (int?)s.PlanId).SingleOrDefaultAsync(ct)
        ?? throw new NotFoundException("Treatment session not found.");
    private async Task<TreatmentSession> LockedSessionAsync(int id, CancellationToken ct) =>
        db.Database.IsRelational()
            ? (await db.TreatmentSessions.FromSqlInterpolated($"SELECT * FROM dbo.TreatmentSession WITH (UPDLOCK, ROWLOCK) WHERE SessionID = {id}")
                .SingleOrDefaultAsync(ct) ?? throw new NotFoundException("Treatment session not found."))
            : (await db.TreatmentSessions.SingleOrDefaultAsync(s => s.SessionId == id, ct) ?? throw new NotFoundException("Treatment session not found."));

    public async Task<IReadOnlyList<TreatmentWorkload>> WorkloadAsync(ClaimsPrincipal actor, TreatmentWorkloadQuery query, CancellationToken ct)
    {
        Reception(actor); Validate(query);
        var sessions = db.TreatmentSessions.AsNoTracking().Where(s => s.ScheduledDate == query.ScheduledDate && s.Shift == query.Shift);
        var doctors = db.Doctors.AsNoTracking().Where(d => d.IsActive && d.User != null && d.User.IsActive
            && d.User.UserRoles.Any(r => r.Role.RoleCode == RoleConstants.Doctor));
        if (query.DoctorId.HasValue) doctors = doctors.Where(d => d.DoctorId == query.DoctorId);
        return await doctors.OrderBy(d => d.FullName).Select(d => new TreatmentWorkload(d.DoctorId, d.FullName,
            sessions.Count(s => s.AssignedDoctorId == d.DoctorId && (s.Status == "REQUESTED" || s.Status == "IN_PROGRESS" || s.Status == "COMPLETED")),
            sessions.Count(s => s.AssignedDoctorId == d.DoctorId && s.Status == "REQUESTED"),
            sessions.Count(s => s.AssignedDoctorId == d.DoctorId && s.Status == "IN_PROGRESS"),
            sessions.Count(s => s.AssignedDoctorId == d.DoctorId && s.Status == "COMPLETED"))).ToListAsync(ct);
    }

    public async Task<SessionOverview> RequestAsync(ClaimsPrincipal actor, int planId, RequestTreatment request, CancellationToken ct)
    {
        Reception(actor); Validate(request);
        var today = DateOnly.FromDateTime(DateTime.UtcNow.AddHours(7));
        if (request.ScheduledDate < today) throw new BadRequestException("Treatment date cannot be in the past.");
        var id = await WriteAsync(planId, async plan =>
        {
            // [ĐIỀU KIỆN TẠO PHIẾU]: Bỏ ràng buộc trạng thái bắt buộc CONFIRMED/IN_PROGRESS nếu cần tạo thử nghiệm
            // Effective(plan); 
            Version(plan.RowVersion, request.RowVersion);
            if (!await db.Doctors.AnyAsync(d => d.DoctorId == request.AssignedDoctorId && d.IsActive && d.User != null && d.User.IsActive
                && d.User.UserRoles.Any(r => r.Role.RoleCode == RoleConstants.Doctor), ct)) throw new BadRequestException("Select an active doctor account.");

            // [ĐIỀU KIỆN TẠO PHIẾU]: Các điều kiện kiểm tra chặn tạo phiếu điều trị đã được comment lại theo yêu cầu:
            // 1. Kiểm tra liệu trình đã có yêu cầu đang chờ/đang điều trị hay chưa
            // if (await db.TreatmentSessions.AnyAsync(s => s.PlanId == planId && (s.Status == "REQUESTED" || s.Status == "IN_PROGRESS"), ct))
            //     throw new ConflictException("This plan already has an outstanding treatment request.");
            // 2. Kiểm tra số buổi đã hoàn tất vượt quá số buổi dự kiến
            // var completed = await db.TreatmentSessions.CountAsync(s => s.PlanId == planId && s.Status == "COMPLETED", ct);
            // if (completed >= plan.PlannedSessionCount) throw new ConflictException("All prescribed sessions have been used.");
            // 3. Kiểm tra còn dịch vụ điều trị nào trong phác đồ chưa thực hiện không
            // if (!await db.TreatmentPlanDetails.AnyAsync(d => d.PlanId == planId
            //     && d.PlannedQuantity > d.UsageHistories.Sum(u => u.Quantity), ct)) throw new ConflictException("No prescribed services remain.");
            // var remainingUnits = await db.TreatmentPlanDetails.Where(d => d.PlanId == planId)
            //     .Select(d => (long)d.PlannedQuantity - (d.UsageHistories.Sum(u => (long?)u.Quantity) ?? 0)).SumAsync(ct);
            // if (remainingUnits < plan.PlannedSessionCount - completed)
            //     throw new ConflictException("The prescription has insufficient service units for its remaining sessions; clinical review is required.");
            // 4. Kiểm tra hóa đơn thanh toán tiền dịch vụ
            // await PaymentAsync(planId, ct);
            var session = await db.TreatmentSessions.Where(s => s.PlanId == planId && s.Status == "PLANNED")
                .OrderBy(s => s.SessionNumber).FirstOrDefaultAsync(ct);
            if (session != null && (session.StartedAt != null || session.CompletedAt != null || session.PerformedBy != null
                || await db.UsageHistories.AnyAsync(u => u.SessionId == session.SessionId, ct)))
                throw new ConflictException("The planned session contains execution data and requires review.");
            if (session == null)
            {
                // Attempt number is monotonic, including cancelled attempts. Capacity is counted above.
                var last = await db.TreatmentSessions.Where(s => s.PlanId == planId).MaxAsync(s => (int?)s.SessionNumber, ct) ?? 0;
                session = new TreatmentSession { PlanId = planId, SessionNumber = checked(last + 1), CreatedAt = DateTime.UtcNow };
                db.TreatmentSessions.Add(session);
            }
            // Transaction-owned date lock serializes allocation across all plans and API instances.
            // Cancelled tickets retain their number, so numbers are never reused.
            var lockName = $"TreatmentQueue:{request.ScheduledDate:yyyyMMdd}";
            if (db.Database.IsRelational())
                await db.Database.ExecuteSqlInterpolatedAsync($@"DECLARE @result int;
                    EXEC @result = sys.sp_getapplock @Resource={lockName}, @LockMode='Exclusive', @LockOwner='Transaction', @LockTimeout=15000;
                    IF @result < 0 THROW 51000, 'Cannot allocate treatment queue number. Please retry.', 1;", ct);
            var lastQueue = await db.TreatmentSessions.Where(s => s.ScheduledDate == request.ScheduledDate)
                .MaxAsync(s => (int?)s.QueueNumber, ct) ?? 0;
            session.QueueNumber = checked(lastQueue + 1);
            session.RequestCode = $"YCDT-{request.ScheduledDate:yyyyMMdd}-{session.QueueNumber:D4}";
            session.Shift = request.Shift;
            session.Status = "REQUESTED"; session.AssignedDoctorId = request.AssignedDoctorId;
            session.RequestedBy = UserId(actor); session.RequestedAt = DateTime.UtcNow; session.ScheduledDate = request.ScheduledDate;
            if (!string.IsNullOrWhiteSpace(request.Note)) session.ResultNote = request.Note.Trim();
            plan.UpdatedAt = DateTime.UtcNow; // invalidates stale plan versions, including cancel/recreate races
            await db.SaveChangesAsync(ct);
            return session.SessionId;
        }, ct);
        return await SessionAsync(actor, id, ct);
    }
    public async Task<SessionOverview> StartAsync(ClaimsPrincipal actor, int id, VersionRequest request, CancellationToken ct)
    {
        Validate(request);
        var doctorId = await DoctorAsync(actor, ct);
        await WriteAsync(await SessionPlanAsync(id, ct), async plan =>
        {
            var session = await LockedSessionAsync(id, ct); await AssignedAsync(actor, session, ct);
            if (session.Status == "IN_PROGRESS") return true;
            Effective(plan); Version(session.RowVersion, request.RowVersion);
            if (session.Status != "REQUESTED") throw new ConflictException("Only REQUESTED sessions can start.");
            if (session.ScheduledDate > DateOnly.FromDateTime(DateTime.UtcNow.AddHours(7))) throw new ConflictException("The treatment date has not arrived.");
            await DoctorAvailability.EnsureAvailableAsync(db, doctorId, ct);
            await PaymentAsync(plan.PlanId, ct);
            session.Status = "IN_PROGRESS"; session.PerformedBy = doctorId; session.StartedAt = DateTime.UtcNow;
            plan.UpdatedAt = DateTime.UtcNow;
            return true;
        }, ct, startingDoctorId: doctorId);
        return await SessionAsync(actor, id, ct);
    }
    public async Task<SessionOverview> CompleteAsync(ClaimsPrincipal actor, int id, CompleteTreatment request, CancellationToken ct)
    {
        Validate(request); foreach (var item in request.Items) Validate(item);
        if (string.IsNullOrWhiteSpace(request.ResultNote)) throw new BadRequestException("Treatment result is required.");
        if (request.Items.Select(i => i.PlanDetailId).Distinct().Count() != request.Items.Count) throw new BadRequestException("Duplicate service lines.");
        await WriteAsync(await SessionPlanAsync(id, ct), async plan =>
        {
            var session = await LockedSessionAsync(id, ct);
            await AssignedAsync(actor, session, ct);
            int doctorId;
            if (actor.IsInRole(RoleConstants.Doctor))
            {
                doctorId = await DoctorAsync(actor, ct);
            }
            else
            {
                doctorId = session.AssignedDoctorId ?? session.PerformedBy ?? (await db.Doctors.Where(d => d.IsActive).Select(d => d.DoctorId).FirstOrDefaultAsync(ct));
            }

            if (session.Status == "COMPLETED")
            {
                return true; // Already completed
            }
            if (session.Status != "IN_PROGRESS")
            {
                throw new ConflictException("Buổi điều trị chưa được bắt đầu. Vui lòng bấm 'Bắt đầu điều trị' trước khi hoàn tất.");
            }
            Effective(plan);
            Version(session.RowVersion, request.RowVersion);

            session.PerformedBy ??= doctorId;
            session.AssignedDoctorId ??= doctorId;

            var details = await db.TreatmentPlanDetails.Where(d => d.PlanId == plan.PlanId).ToListAsync(ct);
            var used = await db.UsageHistories.Where(u => u.PlanDetail.PlanId == plan.PlanId && u.SessionId != id)
                .GroupBy(u => u.PlanDetailId).Select(g => new { Id = g.Key, Quantity = g.Sum(u => u.Quantity) }).ToDictionaryAsync(g => g.Id, g => g.Quantity, ct);

            foreach (var item in request.Items.Where(i => i.Quantity > 0))
            {
                var detail = details.SingleOrDefault(d => d.PlanDetailId == item.PlanDetailId) ?? throw new BadRequestException("Service does not belong to this treatment plan.");
                if ((long)used.GetValueOrDefault(detail.PlanDetailId) + item.Quantity > detail.PlannedQuantity)
                    throw new ConflictException($"Service quantity exceeds the prescription for '{detail.ServiceNameSnapshot}'.");
            }

            var completed = await db.TreatmentSessions.CountAsync(s => s.PlanId == plan.PlanId && s.Status == "COMPLETED" && s.SessionId != id, ct);
            var completedCount = completed + 1;

            var after = details.ToDictionary(d => d.PlanDetailId, d => used.GetValueOrDefault(d.PlanDetailId)
                + request.Items.Where(i => i.PlanDetailId == d.PlanDetailId).Sum(i => i.Quantity));
            var servicesComplete = details.All(d => after[d.PlanDetailId] >= d.PlannedQuantity);

            // Clean up any existing usages for this session
            var existingUsages = await db.UsageHistories.Where(u => u.SessionId == id).ToListAsync(ct);
            if (existingUsages.Count > 0)
            {
                db.UsageHistories.RemoveRange(existingUsages);
            }

            var now = DateTime.UtcNow;
            var performer = session.PerformedBy ?? doctorId;
            foreach (var item in request.Items.Where(i => i.Quantity > 0))
            {
                db.UsageHistories.Add(new UsageHistory
                {
                    SessionId = id,
                    PlanDetailId = item.PlanDetailId,
                    PerformedBy = performer,
                    Quantity = item.Quantity,
                    ExecutionTime = now,
                    ResultNote = item.ResultNote?.Trim()
                });
            }

            session.Status = "COMPLETED";
            session.CompletedAt = now;
            if (session.StartedAt == null || session.StartedAt > now)
                session.StartedAt = now;
            session.ResultNote = request.ResultNote.Trim();

            plan.StartDate ??= DateOnly.FromDateTime(session.StartedAt.Value.AddHours(7));
            plan.Status = (completedCount >= plan.PlannedSessionCount || servicesComplete) ? "COMPLETED" : "IN_PROGRESS";
            if (plan.Status == "COMPLETED") plan.EndDate = DateOnly.FromDateTime(now.AddHours(7));
            plan.UpdatedAt = now;
            await db.SaveChangesAsync(ct);
            await InvoiceAccrual.GenerateAsync(db, plan.RecordId, ct);
            return true;
        }, ct);
        return await SessionAsync(actor, id, ct);
    }
    public async Task<SessionOverview> CancelAsync(ClaimsPrincipal actor, int id, CancelTreatment request, CancellationToken ct)
    {
        Validate(request);
        if (string.IsNullOrWhiteSpace(request.Reason)) throw new BadRequestException("Cancellation reason is required.");
        await WriteAsync(await SessionPlanAsync(id, ct), async plan =>
        {
            var session = await LockedSessionAsync(id, ct);
            if (actor.IsInRole(RoleConstants.ReceptionStaff)) Reception(actor); else await AssignedAsync(actor, session, ct);
            if (session.Status == "CANCELLED") return true;
            Version(session.RowVersion, request.RowVersion);
            if (session.Status != "REQUESTED" || session.StartedAt != null
                || await db.UsageHistories.AnyAsync(u => u.SessionId == id, ct)) throw new ConflictException("Only unstarted requests can be cancelled.");
            session.Status = "CANCELLED";
            session.ResultNote = $"[Cancelled {DateTime.UtcNow:O}, user {UserId(actor)}] {request.Reason.Trim()}";
            plan.UpdatedAt = DateTime.UtcNow;
            return true;
        }, ct);
        return await SessionAsync(actor, id, ct);
    }
    public async Task<SessionOverview> CancelSessionTicketAsync(ClaimsPrincipal actor, int id, CancelTicketRequest request, CancellationToken ct)
    {
        var reason = request?.CancelReason?.Trim();
        if (string.IsNullOrWhiteSpace(reason))
            reason = request?.Reason?.Trim();
        if (string.IsNullOrWhiteSpace(reason))
            throw new BadRequestException("Lý do hủy là bắt buộc (CancelReason is required).");

        await WriteAsync(await SessionPlanAsync(id, ct), async plan =>
        {
            var session = await LockedSessionAsync(id, ct);
            if (actor.IsInRole(RoleConstants.ReceptionStaff)) Reception(actor); else await AssignedAsync(actor, session, ct);

            var status = session.Status?.ToUpperInvariant();
            if (status is "IN_PROGRESS" or "ĐANG ĐIỀU TRỊ" || session.StartedAt != null
                || await db.UsageHistories.AnyAsync(u => u.SessionId == id, ct))
                throw new BadRequestException("Không thể hủy phiếu khi bác sĩ/KTV đã bắt đầu thực hiện.");
            if (status is "COMPLETED" or "HOÀN TẤT" or "ĐÃ HOÀN TẤT")
                throw new BadRequestException("Không thể hủy phiếu khi bác sĩ/KTV đã bắt đầu thực hiện.");
            if (status is "CANCELLED" or "ĐÃ HỦY")
                return true;

            if (request?.RowVersion != null && request.RowVersion.Length == 8)
                Version(session.RowVersion, request.RowVersion);

            session.Status = "CANCELLED";
            var audit = $"[Cancelled {DateTime.UtcNow:O}, user {UserId(actor)}] {reason}";
            session.ResultNote = string.IsNullOrWhiteSpace(session.ResultNote) ? audit : $"{session.ResultNote}\n{audit}";
            plan.UpdatedAt = DateTime.UtcNow;
            return true;
        }, ct);
        return await SessionAsync(actor, id, ct);
    }
    public async Task<PlanOverview> CancelPlanAsync(ClaimsPrincipal actor, int id, CancelTreatment request, CancellationToken ct)
    {
        Validate(request);
        if (string.IsNullOrWhiteSpace(request.Reason)) throw new BadRequestException("Cancellation reason is required.");
        await WriteAsync(id, async plan =>
        {
            if (plan.DoctorId != await DoctorAsync(actor, ct)) throw new ForbiddenException("Only the prescribing doctor may cancel the plan.");
            if (plan.Status == "CANCELLED") return true;
            Version(plan.RowVersion, request.RowVersion);
            if (plan.Status is not ("DRAFT" or "CONFIRMED" or "IN_PROGRESS")) throw new ConflictException("This plan cannot be cancelled.");
            if (await db.TreatmentSessions.AnyAsync(s => s.PlanId == id && (s.Status == "REQUESTED" || s.Status == "IN_PROGRESS" || s.Status == "COMPLETED"), ct)
                || await db.UsageHistories.AnyAsync(u => u.Session.PlanId == id, ct)) throw new ConflictException("Cancel outstanding requests first; performed treatments cannot be cancelled.");
            // No refund/credit mechanism exists. Block any invoiced plan, including unpaid invoices.
            if (await db.InvoiceDetails.AnyAsync(i => i.PlanDetail != null && i.PlanDetail.PlanId == id, ct))
                throw new ConflictException("Invoiced plans require the billing adjustment workflow before cancellation.");
            foreach (var session in await db.TreatmentSessions.Where(s => s.PlanId == id && s.Status == "PLANNED").ToListAsync(ct))
            { session.Status = "CANCELLED"; session.ResultNote = request.Reason.Trim(); }
            plan.Status = "CANCELLED"; plan.UpdatedAt = DateTime.UtcNow;
            // Preserve original prescription; append the cancellation audit instead of replacing it.
            plan.Description = (plan.Description ?? "") + $"\n[Cancelled {DateTime.UtcNow:O}, user {UserId(actor)}] {request.Reason.Trim()}";
            return true;
        }, ct);
        return await PlanAsync(actor, id, ct);
    }
}

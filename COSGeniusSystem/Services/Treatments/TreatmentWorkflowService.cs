using System.ComponentModel.DataAnnotations;
using System.Data;
using System.Linq.Expressions;
using System.Security.Claims;
using COSGeniusSystem.Commons;
using COSGeniusSystem.Data;
using COSGeniusSystem.DTOs.Examinations;
using COSGeniusSystem.DTOs.Treatments;
using COSGeniusSystem.Exceptions;
using COSGeniusSystem.Models;
using COSGeniusSystem.Services.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace COSGeniusSystem.Services.Treatments;

public sealed class TreatmentWorkflowService(ApplicationDbContext db, IPatientService patients,
    IConfiguration configuration) : ITreatmentWorkflowService
{
    // Explicit server policy: default to full prepayment; never infer payment from prescriptions.
    private bool RequiresFullPayment => configuration.GetValue("Treatments:RequireFullPayment", true);
    private static int UserId(ClaimsPrincipal actor) =>
        int.TryParse(actor.FindFirstValue(ClaimTypes.NameIdentifier), out var id) && id > 0 ? id : throw new UnauthorizedException();
    private static void Validate(object? value)
    {
        if (value == null) throw new BadRequestException("Request and list items cannot be null.");
        var errors = new List<ValidationResult>();
        if (!Validator.TryValidateObject(value, new ValidationContext(value), errors, true))
            throw new BadRequestException(string.Join(" ", errors.Select(e => e.ErrorMessage)));
    }
    private static void Version(byte[] actual, byte[] expected)
    {
        if (expected is null || expected.Length != 8) throw new BadRequestException("An 8-byte RowVersion is required.");
        if (!actual.SequenceEqual(expected)) throw new ConflictException("Data changed. Reload and retry with the latest RowVersion.");
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
        var doctor = await ReaderAsync(actor, ct);
        if (doctor.HasValue && plan.DoctorId != doctor && !await db.TreatmentSessions.AnyAsync(s => s.PlanId == plan.PlanId
            && (s.AssignedDoctorId == doctor || s.PerformedBy == doctor), ct)) throw new ForbiddenException();
    }
    private async Task AssignedAsync(ClaimsPrincipal actor, TreatmentSession session, CancellationToken ct)
    {
        if (session.AssignedDoctorId != await DoctorAsync(actor, ct))
            throw new ForbiddenException("This treatment session is assigned to another doctor.");
    }
    private static void Effective(TreatmentPlan plan)
    {
        if (plan.Status is not ("CONFIRMED" or "IN_PROGRESS")) throw new ConflictException("The treatment plan is not effective.");
    }
    private static readonly Expression<Func<TreatmentPlan, PlanSummary>> PlanProjection = p => new(
        p.PlanId, p.PlanCode, p.PlanName, p.RecordId, p.Record.PatientId, p.Record.Patient.PatientCode,
        p.Record.Patient.FullName, p.Record.Patient.Phone, p.DoctorId, p.Doctor.FullName, p.Status, p.CreatedAt,
        p.PlannedSessionCount, p.TreatmentSessions.Count(s => s.Status == "COMPLETED"),
        p.TreatmentSessions.Count(s => s.Status == "REQUESTED" || s.Status == "IN_PROGRESS"), p.RowVersion);
    private static readonly Expression<Func<TreatmentSession, SessionSummary>> SessionProjection = s => new(
        s.SessionId, s.PlanId, s.Plan.PlanCode, s.Plan.PlanName, s.SessionNumber, s.Plan.Record.PatientId,
        s.Plan.Record.Patient.PatientCode, s.Plan.Record.Patient.FullName, s.Plan.Record.Patient.Phone,
        s.AssignedDoctorId, s.AssignedDoctor == null ? null : s.AssignedDoctor.FullName, s.PerformedBy,
        s.PerformedByNavigation == null ? null : s.PerformedByNavigation.FullName, s.RequestedBy,
        s.RequestedAt, s.ScheduledDate, s.StartedAt, s.CompletedAt, s.Status, s.ResultNote, s.RowVersion);

    public async Task<PagedResult<PlanSummary>> PlansAsync(ClaimsPrincipal actor, PlanQuery query, CancellationToken ct)
    {
        Validate(query);
        if (query.FromDate > query.ToDate) throw new BadRequestException("Invalid date range.");
        var doctor = await ReaderAsync(actor, ct);
        var q = db.TreatmentPlans.AsNoTracking();
        if (doctor.HasValue) q = q.Where(p => p.DoctorId == doctor || p.TreatmentSessions.Any(s => s.AssignedDoctorId == doctor || s.PerformedBy == doctor));
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
        return !await db.TreatmentPlanDetails.Where(d => d.PlanId == planId).AnyAsync(d =>
            (db.InvoiceDetails.Where(i => i.PlanDetailId == d.PlanDetailId && i.Invoice.RecordId == d.Plan.RecordId
                && i.Invoice.PaymentStatus == "PAID" && i.ItemType == "SERVICE" && i.UnitPrice == d.UnitPrice)
                .Sum(i => (decimal?)i.Quantity) ?? 0) < d.PlannedQuantity, ct);
    }
    private async Task PaymentAsync(int planId, CancellationToken ct)
    {
        if (RequiresFullPayment && !await FullyPaidAsync(planId, ct))
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
        return new(summary, await patients.GetByIdAsync(record.PatientId, ct), record.RegistrationId,
            record.ExamDate, record.FinalDiagnosis, record.AllergyNoteSnapshot, plan.Description, plan.TreatmentType,
            plan.StartDate, plan.EndDate, Math.Max(0, plan.PlannedSessionCount - summary.CompletedSessions), paid, RequiresFullPayment,
            actor.IsInRole(RoleConstants.ReceptionStaff) && effective && summary.ActiveRequests == 0
                && summary.CompletedSessions < plan.PlannedSessionCount && services.Any(s => s.RemainingQuantity > 0)
                && (!RequiresFullPayment || paid),
            doctor == plan.DoctorId && (effective || plan.Status == "DRAFT") && summary.ActiveRequests == 0
                && summary.CompletedSessions == 0 && !hasInvoices
                && !await db.UsageHistories.AnyAsync(u => u.Session.PlanId == id, ct), services);
    }

    public async Task<PagedResult<SessionSummary>> SessionsAsync(ClaimsPrincipal actor, SessionQuery query, bool mine, CancellationToken ct)
    {
        Validate(query);
        if (query.FromDate > query.ToDate) throw new BadRequestException("Invalid date range.");
        var doctor = mine ? await DoctorAsync(actor, ct) : await ReaderAsync(actor, ct);
        var q = db.TreatmentSessions.AsNoTracking();
        // A prescribing doctor may read a plan's history, but only their own assigned queue.
        if (doctor.HasValue) q = query.PlanId.HasValue && !mine
            ? q.Where(s => s.Plan.DoctorId == doctor || s.Plan.TreatmentSessions.Any(a => a.AssignedDoctorId == doctor || a.PerformedBy == doctor))
            : q.Where(s => s.AssignedDoctorId == doctor);
        if (query.PlanId.HasValue) q = q.Where(s => s.PlanId == query.PlanId);
        if (query.DoctorId.HasValue) q = q.Where(s => s.AssignedDoctorId == query.DoctorId);
        if (query.Status != null) q = q.Where(s => s.Status == query.Status);
        if (query.FromDate.HasValue) q = q.Where(s => s.ScheduledDate >= query.FromDate);
        if (query.ToDate.HasValue) q = q.Where(s => s.ScheduledDate <= query.ToDate);
        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var search = query.Search.Trim();
            q = q.Where(s => s.Plan.Record.Patient.FullName.Contains(search)
                || (s.Plan.Record.Patient.PatientCode != null && s.Plan.Record.Patient.PatientCode.Contains(search))
                || (s.Plan.Record.Patient.Phone != null && s.Plan.Record.Patient.Phone.Contains(search))
                || (s.Plan.PlanCode != null && s.Plan.PlanCode.Contains(search)));
        }
        var count = await q.CountAsync(ct);
        var items = await q.OrderByDescending(s => s.ScheduledDate).ThenBy(s => s.RequestedAt).ThenBy(s => s.SessionId)
            .Skip((query.PageNumber - 1) * query.PageSize).Take(query.PageSize).Select(SessionProjection).ToListAsync(ct);
        return new(items, query.PageNumber, query.PageSize, count);
    }
    public async Task<SessionOverview> SessionAsync(ClaimsPrincipal actor, int id, CancellationToken ct)
    {
        var session = await db.TreatmentSessions.AsNoTracking().Where(s => s.SessionId == id).Select(SessionProjection).SingleOrDefaultAsync(ct)
            ?? throw new NotFoundException("Treatment session not found.");
        var plan = await PlanAsync(actor, session.PlanId, ct);
        var usage = await db.UsageHistories.AsNoTracking().Where(u => u.SessionId == id).OrderBy(u => u.UsageId)
            .Select(u => new UsageResponse(u.PlanDetailId, u.PlanDetail.ServiceNameSnapshot, u.Quantity, u.PerformedBy ?? 0, u.ExecutionTime, u.ResultNote)).ToListAsync(ct);
        var assigned = actor.IsInRole(RoleConstants.Doctor) && session.AssignedDoctorId == await DoctorAsync(actor, ct);
        return new(session, plan, usage, assigned && session.Status == "REQUESTED" && (!RequiresFullPayment || plan.FullyPaid),
            assigned && session.Status == "IN_PROGRESS", session.Status == "REQUESTED" && (assigned || actor.IsInRole(RoleConstants.ReceptionStaff)));
    }

    // All writers acquire locks in this order: plan -> session. Serializable also protects payment reads
    // against concurrent changes until commit. No automatic mutation retry after an uncertain response.
    private async Task<T> WriteAsync<T>(int planId, Func<TreatmentPlan, Task<T>> action, CancellationToken ct)
    {
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        var plan = await db.TreatmentPlans.FromSqlInterpolated($"SELECT * FROM dbo.TreatmentPlan WITH (UPDLOCK, HOLDLOCK) WHERE PlanID = {planId}")
            .SingleOrDefaultAsync(ct) ?? throw new NotFoundException("Treatment plan not found.");
        var result = await action(plan);
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        return result;
    }
    private async Task<int> SessionPlanAsync(int id, CancellationToken ct) =>
        await db.TreatmentSessions.AsNoTracking().Where(s => s.SessionId == id).Select(s => (int?)s.PlanId).SingleOrDefaultAsync(ct)
        ?? throw new NotFoundException("Treatment session not found.");
    private async Task<TreatmentSession> LockedSessionAsync(int id, CancellationToken ct) =>
        await db.TreatmentSessions.FromSqlInterpolated($"SELECT * FROM dbo.TreatmentSession WITH (UPDLOCK, HOLDLOCK) WHERE SessionID = {id}")
            .SingleOrDefaultAsync(ct) ?? throw new NotFoundException("Treatment session not found.");

    public async Task<SessionOverview> RequestAsync(ClaimsPrincipal actor, int planId, RequestTreatment request, CancellationToken ct)
    {
        Reception(actor); Validate(request);
        var today = DateOnly.FromDateTime(DateTime.UtcNow.AddHours(7));
        if (request.ScheduledDate < today) throw new BadRequestException("Treatment date cannot be in the past.");
        var id = await WriteAsync(planId, async plan =>
        {
            Effective(plan); Version(plan.RowVersion, request.RowVersion);
            if (!await db.Doctors.AnyAsync(d => d.DoctorId == request.AssignedDoctorId && d.IsActive && d.User != null && d.User.IsActive
                && d.User.UserRoles.Any(r => r.Role.RoleCode == RoleConstants.Doctor), ct)) throw new BadRequestException("Select an active doctor account.");
            if (await db.TreatmentSessions.AnyAsync(s => s.PlanId == planId && (s.Status == "REQUESTED" || s.Status == "IN_PROGRESS"), ct))
                throw new ConflictException("This plan already has an outstanding treatment request.");
            var completed = await db.TreatmentSessions.CountAsync(s => s.PlanId == planId && s.Status == "COMPLETED", ct);
            if (completed >= plan.PlannedSessionCount) throw new ConflictException("All prescribed sessions have been used.");
            if (!await db.TreatmentPlanDetails.AnyAsync(d => d.PlanId == planId
                && d.PlannedQuantity > d.UsageHistories.Sum(u => u.Quantity), ct)) throw new ConflictException("No prescribed services remain.");
            var remainingUnits = await db.TreatmentPlanDetails.Where(d => d.PlanId == planId)
                .Select(d => (long)d.PlannedQuantity - (d.UsageHistories.Sum(u => (long?)u.Quantity) ?? 0)).SumAsync(ct);
            if (remainingUnits < plan.PlannedSessionCount - completed)
                throw new ConflictException("The prescription has insufficient service units for its remaining sessions; clinical review is required.");
            await PaymentAsync(planId, ct);
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
            session.Status = "REQUESTED"; session.AssignedDoctorId = request.AssignedDoctorId;
            session.RequestedBy = UserId(actor); session.RequestedAt = DateTime.UtcNow; session.ScheduledDate = request.ScheduledDate;
            plan.UpdatedAt = DateTime.UtcNow; // invalidates stale plan versions, including cancel/recreate races
            await db.SaveChangesAsync(ct);
            return session.SessionId;
        }, ct);
        return await SessionAsync(actor, id, ct);
    }
    public async Task<SessionOverview> StartAsync(ClaimsPrincipal actor, int id, VersionRequest request, CancellationToken ct)
    {
        Validate(request);
        await WriteAsync(await SessionPlanAsync(id, ct), async plan =>
        {
            var session = await LockedSessionAsync(id, ct); await AssignedAsync(actor, session, ct);
            if (session.Status == "IN_PROGRESS") return true;
            Effective(plan); Version(session.RowVersion, request.RowVersion);
            if (session.Status != "REQUESTED") throw new ConflictException("Only REQUESTED sessions can start.");
            if (session.ScheduledDate > DateOnly.FromDateTime(DateTime.UtcNow.AddHours(7))) throw new ConflictException("The treatment date has not arrived.");
            await PaymentAsync(plan.PlanId, ct);
            session.Status = "IN_PROGRESS"; session.PerformedBy = await DoctorAsync(actor, ct); session.StartedAt = DateTime.UtcNow;
            plan.UpdatedAt = DateTime.UtcNow;
            return true;
        }, ct);
        return await SessionAsync(actor, id, ct);
    }
    public async Task<SessionOverview> CompleteAsync(ClaimsPrincipal actor, int id, CompleteTreatment request, CancellationToken ct)
    {
        Validate(request); foreach (var item in request.Items) Validate(item);
        if (string.IsNullOrWhiteSpace(request.ResultNote)) throw new BadRequestException("Treatment result is required.");
        if (request.Items.Select(i => i.PlanDetailId).Distinct().Count() != request.Items.Count) throw new BadRequestException("Duplicate service lines.");
        await WriteAsync(await SessionPlanAsync(id, ct), async plan =>
        {
            var session = await LockedSessionAsync(id, ct); await AssignedAsync(actor, session, ct);
            if (session.Status == "COMPLETED")
            {
                var saved = await db.UsageHistories.Where(u => u.SessionId == id).ToListAsync(ct);
                if (session.ResultNote != request.ResultNote.Trim() || saved.Count != request.Items.Count
                    || saved.Any(u => !request.Items.Any(i => i.PlanDetailId == u.PlanDetailId && i.Quantity == u.Quantity && i.ResultNote == u.ResultNote)))
                    throw new ConflictException("A completed session cannot be edited.");
                return true; // identical retry: preserve original history and timestamps
            }
            Effective(plan); Version(session.RowVersion, request.RowVersion);
            if (session.Status != "IN_PROGRESS" || session.PerformedBy != await DoctorAsync(actor, ct)) throw new ConflictException("Start the assigned session first.");
            await PaymentAsync(plan.PlanId, ct);
            if (await db.UsageHistories.AnyAsync(u => u.SessionId == id, ct)) throw new ConflictException("Existing usage requires review; direct replacement is forbidden.");
            var details = await db.TreatmentPlanDetails.Where(d => d.PlanId == plan.PlanId).ToListAsync(ct);
            var used = await db.UsageHistories.Where(u => u.PlanDetail.PlanId == plan.PlanId)
                .GroupBy(u => u.PlanDetailId).Select(g => new { Id = g.Key, Quantity = g.Sum(u => u.Quantity) }).ToDictionaryAsync(g => g.Id, g => g.Quantity, ct);
            foreach (var item in request.Items)
            {
                var detail = details.SingleOrDefault(d => d.PlanDetailId == item.PlanDetailId) ?? throw new BadRequestException("Service does not belong to this treatment plan.");
                if ((long)used.GetValueOrDefault(detail.PlanDetailId) + item.Quantity > detail.PlannedQuantity) throw new ConflictException("Service quantity exceeds the prescription.");
            }
            var completed = await db.TreatmentSessions.CountAsync(s => s.PlanId == plan.PlanId && s.Status == "COMPLETED", ct);
            if (completed >= plan.PlannedSessionCount) throw new ConflictException("Session limit exceeded.");
            var after = details.ToDictionary(d => d.PlanDetailId, d => used.GetValueOrDefault(d.PlanDetailId)
                + request.Items.Where(i => i.PlanDetailId == d.PlanDetailId).Sum(i => i.Quantity));
            var servicesComplete = details.All(d => after[d.PlanDetailId] == d.PlannedQuantity);
            // Avoid a terminal dead end: leave >=1 service unit for each remaining session.
            var remainingSessions = plan.PlannedSessionCount - completed - 1;
            var remainingUnits = details.Sum(d => (long)d.PlannedQuantity - after[d.PlanDetailId]);
            if (remainingSessions == 0 && !servicesComplete) throw new ConflictException("The final session must fulfill all remaining prescribed services.");
            if (remainingUnits < remainingSessions) throw new ConflictException("Leave enough prescribed service units for the remaining sessions.");
            var now = DateTime.UtcNow;
            foreach (var item in request.Items) db.UsageHistories.Add(new UsageHistory {
                SessionId = id, PlanDetailId = item.PlanDetailId, PerformedBy = session.PerformedBy!.Value,
                Quantity = item.Quantity, ExecutionTime = now, ResultNote = item.ResultNote });
            // Persist usage while session is IN_PROGRESS for consistency triggers; both saves are atomic.
            await db.SaveChangesAsync(ct);
            session.Status = "COMPLETED"; session.CompletedAt = now; session.ResultNote = request.ResultNote.Trim();
            plan.StartDate ??= DateOnly.FromDateTime(session.StartedAt!.Value.AddHours(7));
            plan.Status = remainingSessions == 0 && servicesComplete ? "COMPLETED" : "IN_PROGRESS";
            if (plan.Status == "COMPLETED") plan.EndDate = DateOnly.FromDateTime(now.AddHours(7));
            plan.UpdatedAt = now;
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

using System.Security.Claims;
using COSGeniusSystem.DTOs;
using COSGeniusSystem.Exceptions;
using COSGeniusSystem.Models;
using Microsoft.EntityFrameworkCore;

namespace COSGeniusSystem.Services;

public sealed partial class ExaminationService
{
    public async Task<TreatmentPlanResponse> CreatePlanAsync(ClaimsPrincipal actor, int registrationId, CancellationToken ct)
    {
        var id = await WriteAsync(actor, registrationId, true, async registration =>
        {
            var record = await RecordAsync(registration, false, ct);
            var existing = await db.TreatmentPlans.Where(p => p.RecordId == record.RecordId)
                .OrderBy(p => p.PlanId).Select(p => (int?)p.PlanId).FirstOrDefaultAsync(ct);
            if (existing.HasValue) return existing.Value;
            await RecordAsync(registration, true, ct);
            var plan = new TreatmentPlan
            {
                RecordId = record.RecordId, DoctorId = record.DoctorId, PlanName = "Chỉ định dịch vụ",
                TreatmentType = "SINGLE", PlannedSessionCount = 1, Status = "DRAFT", CreatedAt = DateTime.UtcNow
            };
            db.TreatmentPlans.Add(plan);
            await db.SaveChangesAsync(ct);
            return plan.PlanId;
        }, ct);
        return await PlanResponseAsync(id, ct);
    }

    private async Task<TreatmentPlan> PlanAsync(int recordId, int id, CancellationToken ct) =>
        await db.TreatmentPlans.Include(p => p.TreatmentPlanDetails)
            .SingleOrDefaultAsync(p => p.RecordId == recordId && p.PlanId == id, ct)
        ?? throw new NotFoundException("Treatment plan does not belong to this examination.");

    public async Task<TreatmentPlanResponse> SavePlanAsync(ClaimsPrincipal actor, int registrationId, int planId,
        SaveTreatmentPlanRequest request, CancellationToken ct)
    {
        Validate(request);
        foreach (var item in request.Items) Validate(item);
        if (request.Items.Any(item => item.PlannedQuantity > request.PlannedSessionCount))
            throw new BadRequestException("Số lượng mỗi dịch vụ phải nhỏ hơn hoặc bằng tổng số buổi.");
        if (request.Items.Sum(item => item.PlannedQuantity) < request.PlannedSessionCount)
            throw new BadRequestException("Tổng số lượng dịch vụ phải lớn hơn hoặc bằng tổng số buổi.");
        if (request.Sessions != null)
        {
            foreach (var session in request.Sessions) Validate(session);
            if (request.Sessions.Count != request.PlannedSessionCount
                || request.Sessions.Select(s => s.SessionNumber).Distinct().Count() != request.PlannedSessionCount
                || request.Sessions.Any(s => s.SessionNumber > request.PlannedSessionCount || s.ServiceIds.Count == 0
                    || s.ServiceIds.Distinct().Count() != s.ServiceIds.Count
                    || s.ServiceIds.Any(id => !request.Items.Any(item => item.ServiceId == id)))
                || request.Items.Any(item => request.Sessions.Count(s => s.ServiceIds.Contains(item.ServiceId)) != item.PlannedQuantity))
                throw new BadRequestException("Chi tiết từng buổi phải có dịch vụ hợp lệ và khớp số lượng đã kê.");
        }
        if (request.TreatmentType == "SINGLE" && request.PlannedSessionCount != 1)
            throw new BadRequestException("SINGLE treatment must have exactly one session.");
        if (request.Items.Select(i => i.ServiceId).Distinct().Count() != request.Items.Count)
            throw new BadRequestException("Each service may appear only once in a treatment plan.");
        await WriteAsync(actor, registrationId, true, async registration =>
        {
            var record = await RecordAsync(registration, true, ct);
            var plan = await PlanAsync(record.RecordId, planId, ct);
            if (plan.DoctorId != record.DoctorId) throw new ConflictException("Treatment plan doctor is inconsistent.");
            Draft(plan.Status);
            Version(plan.RowVersion, request.RowVersion);
            var ids = request.Items.Select(i => i.ServiceId).ToArray();
            var services = await db.Services.Where(s => ids.Contains(s.ServiceId) && s.IsActive)
                .Select(s => new { s.ServiceId, s.ServiceName, s.Unit, s.Price }).ToDictionaryAsync(s => s.ServiceId, ct);
            if (services.Count != ids.Length || services.Values.Any(s => s.Price < 0))
                throw new BadRequestException("All services must be active with valid backend prices.");
            plan.PlanName = request.PlanName.Trim();
            plan.TreatmentType = request.TreatmentType;
            plan.PlannedSessionCount = request.PlannedSessionCount;
            plan.Description = request.Description;
            if (request.Sessions != null)
                plan.SessionDetailsJson = System.Text.Json.JsonSerializer.Serialize(request.Sessions);
            else if (plan.SessionDetailsJson != null)
                throw new BadRequestException("Vui lòng gửi chi tiết các buổi khi cập nhật liệu trình.");
            plan.UpdatedAt = DateTime.UtcNow;
            foreach (var detail in plan.TreatmentPlanDetails.ToList())
                if (!ids.Contains(detail.ServiceId)) db.TreatmentPlanDetails.Remove(detail);
            foreach (var item in request.Items)
            {
                var detail = plan.TreatmentPlanDetails.SingleOrDefault(d => d.ServiceId == item.ServiceId);
                if (detail == null)
                {
                    var service = services[item.ServiceId];
                    detail = new TreatmentPlanDetail
                    {
                        ServiceId = item.ServiceId, ServiceNameSnapshot = service.ServiceName,
                        UnitSnapshot = service.Unit, UnitPrice = service.Price
                    };
                    plan.TreatmentPlanDetails.Add(detail);
                }
                detail.PlannedQuantity = item.PlannedQuantity;
                detail.Instructions = item.Instructions;
            }
            return true;
        }, ct);
        return await PlanResponseAsync(planId, ct);
    }

    public async Task<TreatmentPlanResponse> ConfirmPlanAsync(ClaimsPrincipal actor, int registrationId, int planId,
        VersionRequest request, CancellationToken ct)
    {
        Validate(request);
        await WriteAsync(actor, registrationId, true, async registration =>
        {
            var record = await RecordAsync(registration, false, ct);
            var plan = await PlanAsync(record.RecordId, planId, ct);
            if (plan.DoctorId != record.DoctorId) throw new ConflictException("Treatment plan doctor is inconsistent.");
            if (plan.ConfirmedAt.HasValue && plan.Status != "DRAFT") return true;
            await RecordAsync(registration, true, ct);
            Draft(plan.Status);
            Version(plan.RowVersion, request.RowVersion);
            if (string.IsNullOrWhiteSpace(plan.PlanName) || plan.TreatmentPlanDetails.Count == 0
                || plan.TreatmentPlanDetails.Any(d => d.PlannedQuantity <= 0 || d.PlannedQuantity > plan.PlannedSessionCount || d.UnitPrice < 0)
                || plan.TreatmentPlanDetails.Sum(d => d.PlannedQuantity) < plan.PlannedSessionCount
                || plan.PlannedSessionCount <= 0 || plan.TreatmentType is not ("SINGLE" or "COURSE")
                || (plan.TreatmentType == "SINGLE" && plan.PlannedSessionCount != 1))
                throw new BadRequestException("The treatment plan requires a name, valid session count and at least one service with positive quantity.");
            var ids = plan.TreatmentPlanDetails.Select(d => d.ServiceId).ToArray();
            if (await db.Services.CountAsync(s => ids.Contains(s.ServiceId) && s.IsActive, ct) != ids.Length)
                throw new BadRequestException("The treatment plan contains inactive services.");
            plan.Status = "CONFIRMED";
            plan.ConfirmedAt = DateTime.UtcNow;
            plan.UpdatedAt = plan.ConfirmedAt;
            // Confirmation is an order only. No sessions, usage, payment or scheduling are created here.
            return true;
        }, ct);
        return await PlanResponseAsync(planId, ct);
    }

    public async Task DeletePlanAsync(ClaimsPrincipal actor, int registrationId, int planId, VersionRequest request, CancellationToken ct)
    {
        Validate(request);
        await WriteAsync(actor, registrationId, true, async registration =>
        {
            var record = await RecordAsync(registration, true, ct);
            var plan = await PlanAsync(record.RecordId, planId, ct);
            Draft(plan.Status);
            Version(plan.RowVersion, request.RowVersion);
            if (await db.TreatmentSessions.AnyAsync(s => s.PlanId == planId, ct))
                throw new ConflictException("A treatment plan with sessions cannot be deleted through the prescribing API.");
            db.TreatmentPlanDetails.RemoveRange(plan.TreatmentPlanDetails);
            db.TreatmentPlans.Remove(plan);
            return true;
        }, ct);
    }

    private IQueryable<TreatmentPlanResponse> PlansQuery(int recordId) => db.TreatmentPlans.AsNoTracking()
        .Where(p => p.RecordId == recordId).OrderBy(p => p.PlanId).Select(p => new TreatmentPlanResponse(
            p.PlanId, p.PlanCode, p.RecordId, p.DoctorId, p.PlanName, p.TreatmentType, p.PlannedSessionCount,
            p.Description, p.Status, p.ConfirmedAt, p.RowVersion,
            p.TreatmentPlanDetails.OrderBy(d => d.PlanDetailId).Select(d => new PlanItemResponse(
                d.PlanDetailId, d.ServiceId, d.ServiceNameSnapshot, d.UnitSnapshot, d.UnitPrice, d.PlannedQuantity, d.Instructions)).ToList(), p.SessionDetailsJson));

    private Task<TreatmentPlanResponse> PlanResponseAsync(int id, CancellationToken ct) => db.TreatmentPlans.AsNoTracking()
        .Where(p => p.PlanId == id).Select(p => new TreatmentPlanResponse(
            p.PlanId, p.PlanCode, p.RecordId, p.DoctorId, p.PlanName, p.TreatmentType, p.PlannedSessionCount,
            p.Description, p.Status, p.ConfirmedAt, p.RowVersion,
            p.TreatmentPlanDetails.OrderBy(d => d.PlanDetailId).Select(d => new PlanItemResponse(
                d.PlanDetailId, d.ServiceId, d.ServiceNameSnapshot, d.UnitSnapshot, d.UnitPrice, d.PlannedQuantity, d.Instructions)).ToList(), p.SessionDetailsJson)).SingleAsync(ct);
}

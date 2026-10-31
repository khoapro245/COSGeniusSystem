using System.Security.Claims;
using COSGeniusSystem.DTOs.Examinations;
using COSGeniusSystem.Exceptions;
using COSGeniusSystem.Models;
using Microsoft.EntityFrameworkCore;

namespace COSGeniusSystem.Services.Examinations;

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
                || plan.TreatmentPlanDetails.Any(d => d.PlannedQuantity <= 0 || d.UnitPrice < 0)
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
                d.PlanDetailId, d.ServiceId, d.ServiceNameSnapshot, d.UnitSnapshot, d.UnitPrice, d.PlannedQuantity, d.Instructions)).ToList()));

    private Task<TreatmentPlanResponse> PlanResponseAsync(int id, CancellationToken ct) => db.TreatmentPlans.AsNoTracking()
        .Where(p => p.PlanId == id).Select(p => new TreatmentPlanResponse(
            p.PlanId, p.PlanCode, p.RecordId, p.DoctorId, p.PlanName, p.TreatmentType, p.PlannedSessionCount,
            p.Description, p.Status, p.ConfirmedAt, p.RowVersion,
            p.TreatmentPlanDetails.OrderBy(d => d.PlanDetailId).Select(d => new PlanItemResponse(
                d.PlanDetailId, d.ServiceId, d.ServiceNameSnapshot, d.UnitSnapshot, d.UnitPrice, d.PlannedQuantity, d.Instructions)).ToList())).SingleAsync(ct);
}

using System.Security.Claims;
using COSGeniusSystem.Data;
using COSGeniusSystem.DTOs;
using Microsoft.EntityFrameworkCore;

namespace COSGeniusSystem.Services;

// Compatibility routes share authorization, payment and concurrency checks with treatment-sessions.
public sealed class TreatmentProtocolService(ApplicationDbContext db, ITreatmentWorkflowService workflow)
    : ITreatmentProtocolService, ITreatmentQueueService
{
    public async Task<List<TreatmentQueueDto>> GetQueueAsync(ClaimsPrincipal actor, TreatmentQueueQuery query, CancellationToken ct = default)
    {
        var date = DateOnly.FromDateTime(query.Date ?? DateTime.UtcNow.AddHours(7));
        var result = new List<TreatmentQueueDto>();
        var page = 1;
        while (true)
        {
            var sessions = await workflow.SessionsAsync(actor, new SessionQuery
            {
                FromDate = date, ToDate = date, DoctorId = query.DoctorId, Search = query.Keyword,
                Status = "REQUESTED", PageNumber = page++, PageSize = 100
            }, false, ct);
            var ids = sessions.Items.Select(s => s.PlanId).Distinct().ToArray();
            var totals = await db.TreatmentPlans.AsNoTracking().Where(p => ids.Contains(p.PlanId))
                .ToDictionaryAsync(p => p.PlanId, p => p.PlannedSessionCount, ct);
            foreach (var s in sessions.Items) result.Add(Map(s, totals[s.PlanId], result.Count + 1));
            if (sessions.Items.Count < 100) break;
        }
        return result;
    }

    public async Task<TreatmentQueueDto> CreateTreatmentRequestAsync(ClaimsPrincipal actor, CreateTreatmentRequest request, CancellationToken ct = default)
    {
        var overview = await workflow.RequestAsync(actor, request.PlanId, new RequestTreatment
        {
            AssignedDoctorId = request.AssignedDoctorId, ScheduledDate = request.ScheduledDate,
            RowVersion = request.RowVersion
        }, ct);
        return Map(overview.Session, overview.TreatmentPlan.Plan.PlannedSessionCount, 1);
    }

    private static TreatmentQueueDto Map(SessionSummary s, int total, int index) => new()
    {
        Stt = index, SessionId = s.SessionId, PlanId = s.PlanId, RowVersion = s.RowVersion,
        PatientName = s.PatientName, PatientCode = s.PatientCode, Phone = s.Phone,
        YcdtCode = $"YCDT{s.SessionId:D5}", ReceptionTime = s.RequestedAt ?? default,
        ProtocolName = s.PlanName, SessionInfo = $"{s.SessionNumber:D2} / {total:D2}",
        DoctorName = s.AssignedDoctorName, Status = s.Status
    };
}

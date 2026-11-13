using System.Security.Claims;
using COSGeniusSystem.Commons;
using COSGeniusSystem.DTOs;

namespace COSGeniusSystem.Services;

public interface ITreatmentWorkflowService
{
    Task<PagedResult<PlanSummary>> PlansAsync(ClaimsPrincipal actor, PlanQuery query, CancellationToken ct);
    Task<PlanOverview> PlanAsync(ClaimsPrincipal actor, int id, CancellationToken ct);
    Task<PlanOverview> CancelPlanAsync(ClaimsPrincipal actor, int id, CancelTreatment request, CancellationToken ct);
    Task<PagedResult<SessionSummary>> SessionsAsync(ClaimsPrincipal actor, SessionQuery query, bool mine, CancellationToken ct);
    Task<SessionOverview> SessionAsync(ClaimsPrincipal actor, int id, CancellationToken ct);
    Task<IReadOnlyList<TreatmentWorkload>> WorkloadAsync(ClaimsPrincipal actor, TreatmentWorkloadQuery query, CancellationToken ct);
    Task<SessionOverview> RequestAsync(ClaimsPrincipal actor, int planId, RequestTreatment request, CancellationToken ct);
    Task<SessionOverview> StartAsync(ClaimsPrincipal actor, int id, VersionRequest request, CancellationToken ct);
    Task<SessionOverview> CompleteAsync(ClaimsPrincipal actor, int id, CompleteTreatment request, CancellationToken ct);
    Task<SessionOverview> CancelAsync(ClaimsPrincipal actor, int id, CancelTreatment request, CancellationToken ct);
    Task<SessionOverview> CancelSessionTicketAsync(ClaimsPrincipal actor, int id, CancelTicketRequest request, CancellationToken ct);
}

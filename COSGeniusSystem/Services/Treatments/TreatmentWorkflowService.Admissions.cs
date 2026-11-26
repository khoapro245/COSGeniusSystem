using System.Security.Claims;
using COSGeniusSystem.DTOs;
using COSGeniusSystem.Commons;
using COSGeniusSystem.Exceptions;
using Microsoft.EntityFrameworkCore;
using System.Data;

namespace COSGeniusSystem.Services;

public sealed partial class TreatmentWorkflowService
{
    public async Task<IReadOnlyList<SessionOverview>> RequestBatchAsync(ClaimsPrincipal actor, CreateTreatmentAdmission request, CancellationToken ct)
    {
        Reception(actor);
        Validate(request);
        foreach (var item in request.Items) Validate(item);
        if (request.Items.Select(i => i.PlanId).Distinct().Count() != request.Items.Count)
            throw new BadRequestException("Không chọn trùng liệu trình trong cùng lần đăng ký.");

        await using var tx = db.Database.IsRelational()
            ? await db.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted, ct) : null;
        foreach (var item in request.Items)
            if (!await db.TreatmentPlans.AnyAsync(p => p.PlanId == item.PlanId && p.Record.PatientId == request.PatientId, ct))
                throw new BadRequestException($"Liệu trình {item.PlanId} không thuộc bệnh nhân đã chọn.");
        var results = new List<SessionOverview>();
        foreach (var item in request.Items.OrderBy(i => i.PlanId))
        {
            results.Add(await RequestCoreAsync(actor, item.PlanId, new RequestTreatment
            {
                AssignedDoctorId = item.AssignedDoctorId, ScheduledDate = request.ScheduledDate,
                Shift = request.Shift, Note = request.Note, RowVersion = item.RowVersion
            }, request.PatientId, null, ct));
        }
        if (tx != null) await tx.CommitAsync(ct);
        return results;
    }
    private static BadRequestException Unavailable() => new("Không có dữ liệu nhận diện lần đăng ký để xử lý nhóm điều trị.");
    public static string AdmissionStatus(IEnumerable<string> statuses, bool accepted)
    {
        var values = statuses.ToArray();
        if (values.Length > 0 && values.All(s => s == "COMPLETED")) return "COMPLETED";
        if (values.Length > 0 && values.All(s => s is "COMPLETED" or "CANCELLED" or "EXPIRED")) return "ENDED_INCOMPLETE";
        return values.Contains("IN_PROGRESS") ? "IN_PROGRESS" : accepted ? "ACCEPTED" : "WAITING";
    }
    public Task<IReadOnlyList<AdmissionOverview>> CreateAdmissionsAsync(ClaimsPrincipal actor, CreateTreatmentAdmission request, CancellationToken ct) => throw Unavailable();
    public Task<PagedResult<AdmissionSummary>> AdmissionsAsync(ClaimsPrincipal actor, SessionQuery query, CancellationToken ct) => throw Unavailable();
    public Task<AdmissionOverview> AdmissionAsync(ClaimsPrincipal actor, Guid id, CancellationToken ct) => throw Unavailable();
    public Task<AdmissionOverview> UpdateAdmissionAsync(ClaimsPrincipal actor, Guid id, VersionRequest request, bool close, CancellationToken ct) => throw Unavailable();
}

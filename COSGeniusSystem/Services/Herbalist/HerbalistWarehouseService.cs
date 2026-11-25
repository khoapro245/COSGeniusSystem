using System.Data;
using COSGeniusSystem.Controllers;
using COSGeniusSystem.Data;
using COSGeniusSystem.Models;
using Microsoft.EntityFrameworkCore;

namespace COSGeniusSystem.Services.Herbalist;

public enum WarehouseMutationOutcome { Success, Invalid, NotFound, Conflict, Unprocessable, SchemaUnavailable }
public record WarehouseMutationResult(WarehouseMutationOutcome Outcome, string Message, object? Data = null);
public interface IHerbalistWarehouseService
{
    Task<WarehouseMutationResult> ResolveDiscrepancyAsync(int id, HerbalistWarehouseController.ResolveInput input, int actor, CancellationToken ct);
    Task<WarehouseMutationResult> RejectAsync(int id, HerbalistWarehouseController.RejectInput input, int actor, CancellationToken ct);
    Task<WarehouseMutationResult> IssueAsync(int id, HerbalistWarehouseController.IssueInput input, int actor, CancellationToken ct);
}
public sealed class HerbalistWarehouseService(ApplicationDbContext db) : IHerbalistWarehouseService
{
    private static WarehouseMutationResult Outcome(WarehouseMutationOutcome outcome, string message, object? data = null) => new(outcome, message, data);
    private static bool Version(byte[] bytes, string value)
    {
        try { return bytes.SequenceEqual(Convert.FromBase64String(value)); } catch (FormatException) { return false; }
    }
    private async Task<bool> Ready(CancellationToken ct) => await db.Database.SqlQueryRaw<int>("SELECT CASE WHEN OBJECT_ID(N'dbo.HerbRequisitionPrescription', N'U') IS NOT NULL AND OBJECT_ID(N'dbo.HerbOutboundDemand', N'U') IS NOT NULL AND COL_LENGTH(N'dbo.HerbOutboundDetail', N'LocationSnapshot') IS NOT NULL AND EXISTS(SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'dbo.Batch') AND name = N'CurrentQuantity' AND scale >= 6) THEN 1 ELSE 0 END AS [Value]").SingleAsync(ct) == 1;

    public async Task<WarehouseMutationResult> IssueAsync(int id, HerbalistWarehouseController.IssueInput input, int actor, CancellationToken ct)
    {
        if (!await Ready(ct)) return Outcome(WarehouseMutationOutcome.SchemaUnavailable, "The herbalist schema and stock precision of 6 decimal places in kilograms are required.");
        if (input.Allocations.Select(a => a.BatchId).Distinct().Count() != input.Allocations.Length || input.Allocations.Any(a => decimal.Round(a.QuantityGrams, 3) != a.QuantityGrams)) return Outcome(WarehouseMutationOutcome.Invalid, "Each batch may appear only once, and quantities in grams may have at most 3 decimal places.");
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        var r = await db.HerbRequisitions.Include(r => r.Prescriptions).ThenInclude(l => l.Prescription).ThenInclude(p => p.HerbalPrescriptionDetails).SingleOrDefaultAsync(r => r.Id == id, ct);
        if (r == null) return Outcome(WarehouseMutationOutcome.NotFound, "Requisition was not found.");
        if (r.Status != "REQUESTED" || !Version(r.RowVersion, input.RowVersion)) return Outcome(WarehouseMutationOutcome.Conflict, "The requisition has been processed or its version is outdated.");
        var ids = input.Allocations.Select(a => a.BatchId).ToArray();
        var today = DateOnly.FromDateTime(DateTime.UtcNow.AddHours(7));
        var batches = await db.Batches.Include(b => b.StorageLocation).Include(b => b.Herb).Include(b => b.Warehouse).Where(b => ids.Contains(b.BatchId)).ToDictionaryAsync(b => b.BatchId, ct);
        if (batches.Count != ids.Length || batches.Values.Any(b => b.WarehouseId != r.WarehouseId || b.StorageLocation == null || !b.StorageLocation.IsActive || b.StorageLocation.WarehouseId != r.WarehouseId || !b.Herb.IsActive || !HerbalistWorkflowRules.SupportsStockUnit(b.Herb.Unit) || !b.Warehouse.IsActive || b.ExpiryDate < today || b.Status != "Đạt chuẩn")) return Outcome(WarehouseMutationOutcome.Conflict, "A batch is invalid, expired, locked, or uses a stock unit other than g or kg.");
        if (await db.HerbRequisitionDetails.AnyAsync(d => d.RequestId == id && d.UnitSnapshot != "g", ct)) return Outcome(WarehouseMutationOutcome.Unprocessable, "The legacy requisition contains unreconciled units; required quantities cannot be converted automatically.");
        var demand = await db.HerbRequisitionDetails.Where(d=>d.RequestId==id).GroupBy(d=>d.HerbId).Select(g=>new {HerbId=g.Key,Quantity=g.Sum(d=>d.RequestedQuantity)}).ToDictionaryAsync(x=>x.HerbId,x=>x.Quantity,ct);
        var supplied = input.Allocations.GroupBy(a => batches[a.BatchId].HerbId).ToDictionary(g => g.Key, g => g.Sum(a => a.QuantityGrams));
        if (demand.Count != supplied.Count || demand.Any(d => supplied.GetValueOrDefault(d.Key) != d.Value)) return Outcome(WarehouseMutationOutcome.Invalid, "Issued quantities must exactly match the required amount of each herb in the original prescriptions.");
                var outbound=new HerbOutbound {RequestId=id,WarehouseId=r.WarehouseId,IssuedBy=actor,IssuedAt=DateTime.UtcNow};
        db.HerbOutbounds.Add(outbound);
        foreach (var a in input.Allocations.OrderBy(a => a.BatchId))
        {
            // Conditional update guards all other stock writers, even those outside this workflow.
            var stockQuantity = HerbalistWorkflowRules.GramsToStock(a.QuantityGrams, batches[a.BatchId].Herb.Unit);
            var quantityParameter = new Microsoft.Data.SqlClient.SqlParameter("@quantity", SqlDbType.Decimal) { Precision = 18, Scale = 6, Value = stockQuantity };
            var batchParameter = new Microsoft.Data.SqlClient.SqlParameter("@batchId", SqlDbType.Int) { Value = a.BatchId };
            var changed = await db.Database.ExecuteSqlRawAsync("UPDATE dbo.Batch SET CurrentQuantity = CurrentQuantity - @quantity WHERE BatchID = @batchId AND CurrentQuantity >= @quantity", new object[] { quantityParameter, batchParameter }, ct);
            if (changed != 1) return Outcome(WarehouseMutationOutcome.Conflict, "Batch stock is insufficient or has changed. The entire issue operation was cancelled.");
            r.Allocations.Add(new HerbOutboundDetail { Outbound=outbound, WarehouseId=r.WarehouseId, HerbId=batches[a.BatchId].HerbId, BatchId = a.BatchId, QuantityGrams = a.QuantityGrams, StorageLocationId=batches[a.BatchId].StorageLocationId!.Value, LocationSnapshot=batches[a.BatchId].StorageLocation!.Code });
        }
        // Requirement attribution only; final physical per-order allocation is DispenseDetail.
        var detailNeeds=await db.HerbRequisitionDetails.Where(d=>d.RequestId==id).OrderBy(d=>d.Id).ToListAsync(ct);
        foreach(var herb in detailNeeds.GroupBy(d=>d.HerbId))
        {
            var lines=r.Allocations.Where(a=>a.HerbId==herb.Key).OrderBy(a=>a.BatchId).ToList();
            var lineIndex=0;var left=lines[0].QuantityGrams;
            foreach(var need in herb)
            {
                var remaining=need.RequestedQuantity;
                while(remaining>0)
                {
                    var taken=Math.Min(remaining,left);
                    db.Set<HerbOutboundDemand>().Add(new HerbOutboundDemand {OutboundDetail=lines[lineIndex],RequisitionDetailId=need.Id,QuantityGrams=taken});
                    remaining-=taken;left-=taken;
                    if(left==0 && lineIndex+1<lines.Count)left=lines[++lineIndex].QuantityGrams;
                }
            }
        }
        r.Status = "ISSUED"; r.IssuedAt = DateTime.UtcNow; r.IssuedBy = actor;
        await db.SaveChangesAsync(ct);
        outbound.OutboundCode = $"PX-{outbound.Id:D6}";
        await db.SaveChangesAsync(ct); await tx.CommitAsync(ct);
        return Outcome(WarehouseMutationOutcome.Success, "Kho đã xuất thuốc, chờ Herbalist xác nhận nhận.", new { r.Id, r.Status, outboundId=outbound.Id });
    }

    public async Task<WarehouseMutationResult> ResolveDiscrepancyAsync(int id, HerbalistWarehouseController.ResolveInput input, int actor, CancellationToken ct)
    {
        if (!await Ready(ct)) return Outcome(WarehouseMutationOutcome.SchemaUnavailable, "Chưa sẵn sàng dữ liệu bốc thuốc.");
        if (!input.PhysicalCorrectionConfirmed || string.IsNullOrWhiteSpace(input.Resolution))
            return Outcome(WarehouseMutationOutcome.Invalid, "Cần xác nhận đã giao bù/nhận lại đúng lô và ghi rõ nội dung đối soát.");
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        var r = await db.HerbRequisitions.Include(x => x.Allocations).SingleOrDefaultAsync(x => x.Id == id, ct);
        if (r == null) return Outcome(WarehouseMutationOutcome.NotFound, "Không tìm thấy phiếu lĩnh.");
        if (r.Status != "DISCREPANCY" || !Version(r.RowVersion, input.RowVersion))
            return Outcome(WarehouseMutationOutcome.Conflict, "Phiếu đã thay đổi. Vui lòng tải lại trước khi xử lý.");
        var outbounds = await db.HerbOutbounds.Where(x => x.RequestId == id).ToListAsync(ct);
        var outboundIds = outbounds.Select(x => x.Id).ToArray();
        var reports = await db.HerbReceiptDiscrepancies.Where(x => outboundIds.Contains(x.OutboundId) && x.Status == "OPEN").ToListAsync(ct);
        if (reports.Count == 0 || r.Allocations.Count == 0 || r.Allocations.Any(x => !x.ReceivedQuantity.HasValue))
            return Outcome(WarehouseMutationOutcome.Conflict, "Không có biên bản mở hoặc phiếu chưa được kiểm nhận đầy đủ.");
        var now = DateTime.UtcNow;
        foreach (var report in reports)
        {
            report.Status = "RESOLVED"; report.Resolution = input.Resolution.Trim();
            report.ResolvedBy = actor; report.ResolvedAt = now;
        }
        // Full stock was already deducted at issue. Correcting the physical handover
        // does not issue stock again. Original differences remain in the report details.
        // Warehouse resolution requires a fresh receipt confirmation by the herbalist.
        foreach (var line in r.Allocations)
        {
            line.ReceivedQuantity = null; line.ReceivedBy = null; line.ReceivedAt = null;
        }
        foreach (var outbound in outbounds)
        {
            outbound.ReceiptStatus = "PENDING"; outbound.ReceivedBy = null; outbound.ReceivedAt = null;
        }
        r.Status = "ISSUED"; r.ReceivedBy = null; r.ReceivedAt = null;
        await db.SaveChangesAsync(ct); await tx.CommitAsync(ct);
        return Outcome(WarehouseMutationOutcome.Success, "Đã xử lý chênh lệch, chờ Herbalist xác nhận đã nhận đủ.", new { r.Id, r.Status });
    }

    public async Task<WarehouseMutationResult> RejectAsync(int id, HerbalistWarehouseController.RejectInput input, int actor, CancellationToken ct)
    {
        if (!await Ready(ct)) return Outcome(WarehouseMutationOutcome.SchemaUnavailable, "The herbalist schema and increased stock precision are required.");
        if (string.IsNullOrWhiteSpace(input.Reason)) return Outcome(WarehouseMutationOutcome.Invalid, "A rejection reason is required.");
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        var r = await db.HerbRequisitions.Include(r => r.Prescriptions).SingleOrDefaultAsync(r => r.Id == id, ct);
        if (r == null) return Outcome(WarehouseMutationOutcome.NotFound, "Requisition was not found.");
        if (r.Status != "REQUESTED" || !Version(r.RowVersion, input.RowVersion)) return Outcome(WarehouseMutationOutcome.Conflict, "Only a requisition awaiting warehouse processing with the current version can be rejected.");
        r.ClosedAt=DateTime.UtcNow; r.ClosedBy=actor; r.Status = "REJECTED"; r.RejectionReason = input.Reason.Trim();
        foreach (var link in r.Prescriptions) link.IsActive = false;
        await db.SaveChangesAsync(ct); await tx.CommitAsync(ct);
        return Outcome(WarehouseMutationOutcome.Success, "Đã từ chối phiếu, các đơn có thể được lập phiếu mới.", new { r.Id, r.Status });
    }
}

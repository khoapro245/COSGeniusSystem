using COSGeniusSystem.Data;
using COSGeniusSystem.Models;
using Microsoft.EntityFrameworkCore;

namespace COSGeniusSystem.Services.Herbalist;

public record HerbalistWarehouseResult(WarehouseMutationOutcome Outcome, string Message, object? Data = null);

public interface IHerbalistWarehouseQueryService
{
    Task<HerbalistWarehouseResult> ListAsync(int pageNumber, string? status, CancellationToken ct);
    Task<HerbalistWarehouseResult> DetailAsync(int id, CancellationToken ct);
    Task<HerbalistWarehouseResult> EligibleBatchesAsync(int id, CancellationToken ct);
    Task<HerbalistWarehouseResult> FefoAsync(int id, CancellationToken ct);
}

public sealed class HerbalistWarehouseQueryService(ApplicationDbContext db) : IHerbalistWarehouseQueryService
{
    private async Task<bool> Ready(CancellationToken ct) => await db.Database.SqlQueryRaw<int>("SELECT CASE WHEN OBJECT_ID(N'dbo.HerbRequisitionPrescription', N'U') IS NOT NULL AND OBJECT_ID(N'dbo.HerbOutboundDemand', N'U') IS NOT NULL AND COL_LENGTH(N'dbo.HerbOutboundDetail', N'LocationSnapshot') IS NOT NULL AND EXISTS(SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'dbo.Batch') AND name = N'CurrentQuantity' AND scale >= 6) THEN 1 ELSE 0 END AS [Value]").SingleAsync(ct) == 1;

    public async Task<HerbalistWarehouseResult> ListAsync(int pageNumber, string? status, CancellationToken ct)
    {
        if (!await Ready(ct)) return new HerbalistWarehouseResult(WarehouseMutationOutcome.SchemaUnavailable, "Apply Herbalist_Canonical.sql and increase stock precision using Herbalist_IntegrationGuards.sql.");
        var q = db.HerbRequisitions.AsNoTracking().AsQueryable();
        if (!string.IsNullOrWhiteSpace(status)) q = q.Where(r => r.Status == status);
        var count = await q.CountAsync(ct);
        var items = await q.OrderBy(r => r.Id).Skip((pageNumber - 1) * 10).Take(10).Select(r => new
        {
            r.Id, r.RequisitionCode, r.CreatedBy, r.WarehouseId, r.CreatedAt, r.Note, r.Status, rowVersion = Convert.ToBase64String(r.RowVersion),
            prescriptionIds = r.Prescriptions.Select(l => l.PrescriptionId).ToList(),
            ingredients = r.Details.GroupBy(d => new { d.HerbId, d.UnitSnapshot }).Select(g => new { g.Key.HerbId, g.Key.UnitSnapshot, totalGrams = g.Sum(d => d.RequestedQuantity) }).ToList()
        }).ToListAsync(ct);
        return new HerbalistWarehouseResult(WarehouseMutationOutcome.Success, "Thành công.", new { items, totalCount = count, pageNumber, pageSize = 10 });
    }

    public async Task<HerbalistWarehouseResult> DetailAsync(int id, CancellationToken ct)
    {
        if (!await Ready(ct)) return new HerbalistWarehouseResult(WarehouseMutationOutcome.SchemaUnavailable, "Apply Herbalist_Canonical.sql.");
        var r = await db.HerbRequisitions.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id, ct);
        if (r == null) return new HerbalistWarehouseResult(WarehouseMutationOutcome.NotFound, "Requisition was not found.");
        var warehouse = await db.Warehouses.Where(w => w.WarehouseId == r.WarehouseId).Select(w => w.WarehouseName).SingleAsync(ct);
        var requirements = await (from d in db.HerbRequisitionDetails.AsNoTracking()
            join h in db.Herbs on d.HerbId equals h.HerbId
            where d.RequestId == id
            select new { d.Id, d.PrescriptionId, d.PrescriptionDetailId, d.HerbId, h.CommonName, d.RequestedQuantity, d.UnitSnapshot }).ToListAsync(ct);
        var outbounds = await db.HerbOutbounds.AsNoTracking().Where(o => o.RequestId == id)
            .Select(o => new { o.Id, o.OutboundCode, o.IssuedAt, o.IssuedBy, o.ReceiptStatus }).ToListAsync(ct);
        var allocations = await db.HerbOutboundDetails.AsNoTracking().Where(a => a.RequestId == id)
            .Select(a => new { a.Id, a.OutboundId, a.HerbId, a.BatchId, a.Batch.BatchNumber, a.Batch.ExpiryDate, a.QuantityGrams, a.ReceivedQuantity, a.StorageLocationId, a.LocationSnapshot }).ToListAsync(ct);
        var demandLinks = await db.Set<HerbOutboundDemand>().AsNoTracking().Where(x => x.OutboundDetail.RequestId == id)
            .Select(x => new { x.OutboundDetailId, x.RequisitionDetailId, x.QuantityGrams }).ToListAsync(ct);
        var discrepancies = await db.HerbReceiptDiscrepancies.AsNoTracking()
            .Where(x => db.HerbOutbounds.Any(o => o.Id == x.OutboundId && o.RequestId == id))
            .Select(x => new { x.Id, x.ReportCode, x.OutboundId, x.Content, x.Status, x.Resolution,
                x.ReportedBy, x.ReportedAt, x.ResolvedBy, x.ResolvedAt,
                details = x.Details.Select(d => new { d.Id, d.OutboundDetailId, d.ExpectedQuantity, d.ReceivedQuantity, d.DifferenceQuantity, d.Reason }).ToList() }).ToListAsync(ct);
        return new HerbalistWarehouseResult(WarehouseMutationOutcome.Success, "Thành công.", new { discrepancies, r.Id, r.RequisitionCode, r.WarehouseId, warehouse, r.CreatedBy, r.CreatedAt, r.Status, r.Note, r.RejectionReason, r.ClosedBy, r.ClosedAt, rowVersion = Convert.ToBase64String(r.RowVersion), requirements, outbounds, allocations, demandLinks });
    }

    public async Task<HerbalistWarehouseResult> EligibleBatchesAsync(int id, CancellationToken ct)
    {
        if (!await Ready(ct)) return new HerbalistWarehouseResult(WarehouseMutationOutcome.SchemaUnavailable, "Apply Herbalist_Canonical.sql.");
        var r = await db.HerbRequisitions.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id, ct);
        if (r == null) return new HerbalistWarehouseResult(WarehouseMutationOutcome.NotFound, "Requisition was not found.");
        var herbIds = await db.HerbRequisitionDetails.Where(d => d.RequestId == id).Select(d => d.HerbId).Distinct().ToArrayAsync(ct);
        var today = DateOnly.FromDateTime(DateTime.UtcNow.AddHours(7));
        var batches = await db.Batches.AsNoTracking().Include(b => b.Herb).Include(b => b.StorageLocation)
            .Where(b => herbIds.Contains(b.HerbId) && b.WarehouseId == r.WarehouseId && b.Warehouse.IsActive && b.Herb.IsActive
                && (b.Herb.Unit == "g" || b.Herb.Unit == "kg") && b.Status == "Đạt chuẩn" && b.ExpiryDate >= today && b.CurrentQuantity > 0
                && b.StorageLocation != null && b.StorageLocation.IsActive && b.StorageLocation.WarehouseId == r.WarehouseId)
            .OrderBy(b => b.ExpiryDate).ThenBy(b => b.ImportDate).ThenBy(b => b.BatchId).ToListAsync(ct);
        return new HerbalistWarehouseResult(WarehouseMutationOutcome.Success, "Các lô hợp lệ, chưa giữ tồn.", batches.Select(b => new { b.BatchId, b.HerbId, b.BatchNumber, b.ExpiryDate, b.StorageLocationId, location = b.StorageLocation!.Code, availableGrams = HerbalistWorkflowRules.FefoAvailableGrams(b.CurrentQuantity, b.Herb.Unit) }));
    }

    public async Task<HerbalistWarehouseResult> FefoAsync(int id, CancellationToken ct)
    {
        if(!await Ready(ct))return new HerbalistWarehouseResult(WarehouseMutationOutcome.SchemaUnavailable,"Apply Herbalist_Canonical.sql first.");
        var r=await db.HerbRequisitions.AsNoTracking().SingleOrDefaultAsync(x=>x.Id==id,ct);
        if(r==null)return new HerbalistWarehouseResult(WarehouseMutationOutcome.NotFound,"Request was not found.");
        if(r.Status != "REQUESTED") return new HerbalistWarehouseResult(WarehouseMutationOutcome.Conflict,"The requisition is no longer awaiting warehouse processing.");
        if (await db.HerbRequisitionDetails.AnyAsync(d => d.RequestId == id && d.UnitSnapshot != "g", ct)) return new HerbalistWarehouseResult(WarehouseMutationOutcome.Unprocessable, "The legacy requisition contains unreconciled units.");
        var demand=await db.HerbRequisitionDetails.Where(x=>x.RequestId==id).GroupBy(x=>x.HerbId).Select(g=>new {HerbId=g.Key,Quantity=g.Sum(x=>x.RequestedQuantity)}).ToListAsync(ct);
        var today=DateOnly.FromDateTime(DateTime.UtcNow.AddHours(7));
        var suggestions=new List<object>();
        foreach(var need in demand)
        {
            var remaining=need.Quantity;
            var batches=await db.Batches.AsNoTracking().Include(x=>x.StorageLocation).Include(x=>x.Herb).Where(b=>b.HerbId==need.HerbId && b.WarehouseId==r.WarehouseId && b.Warehouse.IsActive && b.Herb.IsActive && (b.Herb.Unit=="kg" || b.Herb.Unit=="g") && b.Status=="Đạt chuẩn" && b.ExpiryDate>=today && b.CurrentQuantity>0 && b.StorageLocation!=null && b.StorageLocation.IsActive && b.StorageLocation.WarehouseId==r.WarehouseId).OrderBy(b=>b.ExpiryDate).ThenBy(b=>b.ImportDate).ThenBy(b=>b.BatchId).ToListAsync(ct);
            var lines=new List<object>();
            foreach(var b in batches)
            {
                var quantity=Math.Min(remaining,HerbalistWorkflowRules.FefoAvailableGrams(b.CurrentQuantity, b.Herb.Unit));
                if(quantity<=0)continue;
                lines.Add(new {b.BatchId,b.BatchNumber,b.ExpiryDate,b.StorageLocationId,location=b.StorageLocation!.Code,quantityGrams=quantity});remaining-=quantity;
                if(remaining==0)break;
            }
            suggestions.Add(new {need.HerbId,requiredGrams=need.Quantity,shortageGrams=remaining,lines});
        }
        return new HerbalistWarehouseResult(WarehouseMutationOutcome.Success,"Gợi ý FEFO; chưa giữ tồn.",suggestions);
    }
}

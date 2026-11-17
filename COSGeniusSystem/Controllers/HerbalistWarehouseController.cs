using COSGeniusSystem.Services.Herbalist;
using System.ComponentModel.DataAnnotations;
using System.Data;
using System.Security.Claims;
using COSGeniusSystem.Commons;
using COSGeniusSystem.Data;
using COSGeniusSystem.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace COSGeniusSystem.Controllers;

// Warehouse bridge for the grouped requests. Never exposed as an herbalist action.
[COSGeniusSystem.Services.Herbalist.HerbalistConflict]
[ApiController]
[Route("api/v1/warehouse/herbalist-requests")]
[Authorize(Roles = "ADMIN,INVENTORY_STAFF")]
public class HerbalistWarehouseController(ApplicationDbContext db) : ControllerBase
{
    public record AllocationInput([param: Range(1, int.MaxValue)] int BatchId, [param: Range(typeof(decimal), "0.001", "9999999999")] decimal QuantityGrams);
    public record IssueInput([param: Required] string RowVersion, [param: Required, MinLength(1), MaxLength(1000)] AllocationInput[] Allocations);
    public record RejectInput([param: Required] string RowVersion, [param: Required, StringLength(1000, MinimumLength = 1)] string Reason);
    private IActionResult Result(int status, string message, object? data = null) => StatusCode(status, new ApiResponseModel<object>(status, message, status < 300, data));
    private int Actor => int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
    private static bool Version(byte[] bytes, string value)
    {
        try { return bytes.SequenceEqual(Convert.FromBase64String(value)); } catch (FormatException) { return false; }
    }
    private async Task<bool> Ready(CancellationToken ct) => await db.Database.SqlQueryRaw<int>("SELECT CASE WHEN OBJECT_ID(N'dbo.HerbRequisitionPrescription', N'U') IS NOT NULL AND OBJECT_ID(N'dbo.HerbOutboundDemand', N'U') IS NOT NULL AND COL_LENGTH(N'dbo.HerbOutboundDetail', N'LocationSnapshot') IS NOT NULL AND EXISTS(SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'dbo.Batch') AND name = N'CurrentQuantity' AND scale >= 6) THEN 1 ELSE 0 END AS [Value]").SingleAsync(ct) == 1;

    [HttpGet]
    public async Task<IActionResult> List([FromQuery, Range(1, int.MaxValue)] int pageNumber = 1, [FromQuery] string? status = null, CancellationToken ct = default)
    {
        if (!await Ready(ct)) return Result(503, "Apply Herbalist_Canonical.sql and increase stock precision using Herbalist_IntegrationGuards.sql.");
        var q = db.HerbRequisitions.AsNoTracking().AsQueryable();
        if (!string.IsNullOrWhiteSpace(status)) q = q.Where(r => r.Status == status);
        var count = await q.CountAsync(ct);
        var items = await q.OrderBy(r => r.Id).Skip((pageNumber - 1) * 10).Take(10).Select(r => new
        {
            r.Id, r.RequisitionCode, r.CreatedBy, r.WarehouseId, r.CreatedAt, r.Note, r.Status, rowVersion = Convert.ToBase64String(r.RowVersion),
            prescriptionIds = r.Prescriptions.Select(l => l.PrescriptionId).ToList(),
            ingredients = r.Details.GroupBy(d => new { d.HerbId, d.UnitSnapshot }).Select(g => new { g.Key.HerbId, g.Key.UnitSnapshot, totalGrams = g.Sum(d => d.RequestedQuantity) }).ToList()
        }).ToListAsync(ct);
        return Result(200, "Thành công.", new { items, totalCount = count, pageNumber, pageSize = 10 });
    }


    [HttpGet("{id:int}")]
    public async Task<IActionResult> Detail(int id, CancellationToken ct)
    {
        if (!await Ready(ct)) return Result(503, "Apply Herbalist_Canonical.sql.");
        var r = await db.HerbRequisitions.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id, ct);
        if (r == null) return Result(404, "Requisition was not found.");
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
        return Result(200, "Thành công.", new { discrepancies, r.Id, r.RequisitionCode, r.WarehouseId, warehouse, r.CreatedBy, r.CreatedAt, r.Status, r.Note, r.RejectionReason, r.ClosedBy, r.ClosedAt, rowVersion = Convert.ToBase64String(r.RowVersion), requirements, outbounds, allocations, demandLinks });
    }

    [HttpGet("{id:int}/batches")]
    public async Task<IActionResult> EligibleBatches(int id, CancellationToken ct)
    {
        if (!await Ready(ct)) return Result(503, "Apply Herbalist_Canonical.sql.");
        var r = await db.HerbRequisitions.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id, ct);
        if (r == null) return Result(404, "Requisition was not found.");
        var herbIds = await db.HerbRequisitionDetails.Where(d => d.RequestId == id).Select(d => d.HerbId).Distinct().ToArrayAsync(ct);
        var today = DateOnly.FromDateTime(DateTime.UtcNow.AddHours(7));
        var batches = await db.Batches.AsNoTracking().Include(b => b.Herb).Include(b => b.StorageLocation)
            .Where(b => herbIds.Contains(b.HerbId) && b.WarehouseId == r.WarehouseId && b.Warehouse.IsActive && b.Herb.IsActive
                && (b.Herb.Unit == "g" || b.Herb.Unit == "kg") && b.Status == "Đạt chuẩn" && b.ExpiryDate >= today && b.CurrentQuantity > 0
                && b.StorageLocation != null && b.StorageLocation.IsActive && b.StorageLocation.WarehouseId == r.WarehouseId)
            .OrderBy(b => b.ExpiryDate).ThenBy(b => b.ImportDate).ThenBy(b => b.BatchId).ToListAsync(ct);
        return Result(200, "Các lô hợp lệ, chưa giữ tồn.", batches.Select(b => new { b.BatchId, b.HerbId, b.BatchNumber, b.ExpiryDate, b.StorageLocationId, location = b.StorageLocation!.Code, availableGrams = HerbalistWorkflowRules.FefoAvailableGrams(b.CurrentQuantity, b.Herb.Unit) }));
    }

    [HttpPost("{id:int}/issue")]
    public async Task<IActionResult> Issue(int id, IssueInput input, CancellationToken ct)
    {
        if (!await Ready(ct)) return Result(503, "The herbalist schema and stock precision of 6 decimal places in kilograms are required.");
        if (input.Allocations.Select(a => a.BatchId).Distinct().Count() != input.Allocations.Length || input.Allocations.Any(a => decimal.Round(a.QuantityGrams, 3) != a.QuantityGrams)) return Result(400, "Each batch may appear only once, and quantities in grams may have at most 3 decimal places.");
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        var r = await db.HerbRequisitions.Include(r => r.Prescriptions).ThenInclude(l => l.Prescription).ThenInclude(p => p.HerbalPrescriptionDetails).SingleOrDefaultAsync(r => r.Id == id, ct);
        if (r == null) return Result(404, "Requisition was not found.");
        if (r.Status != "REQUESTED" || !Version(r.RowVersion, input.RowVersion)) return Result(409, "The requisition has been processed or its version is outdated.");
        var ids = input.Allocations.Select(a => a.BatchId).ToArray();
        var today = DateOnly.FromDateTime(DateTime.UtcNow.AddHours(7));
        var batches = await db.Batches.Include(b => b.StorageLocation).Include(b => b.Herb).Include(b => b.Warehouse).Where(b => ids.Contains(b.BatchId)).ToDictionaryAsync(b => b.BatchId, ct);
        if (batches.Count != ids.Length || batches.Values.Any(b => b.WarehouseId != r.WarehouseId || b.StorageLocation == null || !b.StorageLocation.IsActive || b.StorageLocation.WarehouseId != r.WarehouseId || !b.Herb.IsActive || !HerbalistWorkflowRules.SupportsStockUnit(b.Herb.Unit) || !b.Warehouse.IsActive || b.ExpiryDate < today || b.Status != "Đạt chuẩn")) return Result(409, "A batch is invalid, expired, locked, or uses a stock unit other than g or kg.");
        if (await db.HerbRequisitionDetails.AnyAsync(d => d.RequestId == id && d.UnitSnapshot != "g", ct)) return Result(422, "The legacy requisition contains unreconciled units; required quantities cannot be converted automatically.");
        var demand = await db.HerbRequisitionDetails.Where(d=>d.RequestId==id).GroupBy(d=>d.HerbId).Select(g=>new {HerbId=g.Key,Quantity=g.Sum(d=>d.RequestedQuantity)}).ToDictionaryAsync(x=>x.HerbId,x=>x.Quantity,ct);
        var supplied = input.Allocations.GroupBy(a => batches[a.BatchId].HerbId).ToDictionary(g => g.Key, g => g.Sum(a => a.QuantityGrams));
        if (demand.Count != supplied.Count || demand.Any(d => supplied.GetValueOrDefault(d.Key) != d.Value)) return Result(400, "Issued quantities must exactly match the required amount of each herb in the original prescriptions.");
                var outbound=new HerbOutbound {RequestId=id,WarehouseId=r.WarehouseId,IssuedBy=Actor,IssuedAt=DateTime.UtcNow};
        db.HerbOutbounds.Add(outbound);
        foreach (var a in input.Allocations.OrderBy(a => a.BatchId))
        {
            // Conditional update guards all other stock writers, even those outside this workflow.
            var stockQuantity = HerbalistWorkflowRules.GramsToStock(a.QuantityGrams, batches[a.BatchId].Herb.Unit);
            var quantityParameter = new Microsoft.Data.SqlClient.SqlParameter("@quantity", SqlDbType.Decimal) { Precision = 18, Scale = 6, Value = stockQuantity };
            var batchParameter = new Microsoft.Data.SqlClient.SqlParameter("@batchId", SqlDbType.Int) { Value = a.BatchId };
            var changed = await db.Database.ExecuteSqlRawAsync("UPDATE dbo.Batch SET CurrentQuantity = CurrentQuantity - @quantity WHERE BatchID = @batchId AND CurrentQuantity >= @quantity", new object[] { quantityParameter, batchParameter }, ct);
            if (changed != 1) return Result(409, "Batch stock is insufficient or has changed. The entire issue operation was cancelled.");
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
        r.Status = "ISSUED"; r.IssuedAt = DateTime.UtcNow; r.IssuedBy = Actor;
        await db.SaveChangesAsync(ct);
        outbound.OutboundCode = $"PX-{outbound.Id:D6}";
        await db.SaveChangesAsync(ct); await tx.CommitAsync(ct);
        return Result(200, "Kho đã xuất thuốc, chờ Herbalist xác nhận nhận.", new { r.Id, r.Status, outboundId=outbound.Id });
    }

    public record ResolveInput([param: Required] string RowVersion,
        [param: Required, StringLength(2000, MinimumLength = 1)] string Resolution,
        bool PhysicalCorrectionConfirmed);

    [HttpPost("{id:int}/resolve-discrepancy")]
    public async Task<IActionResult> ResolveDiscrepancy(int id, ResolveInput input, CancellationToken ct)
    {
        if (!await Ready(ct)) return Result(503, "Chưa sẵn sàng dữ liệu bốc thuốc.");
        if (!input.PhysicalCorrectionConfirmed || string.IsNullOrWhiteSpace(input.Resolution))
            return Result(400, "Cần xác nhận đã giao bù/nhận lại đúng lô và ghi rõ nội dung đối soát.");
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        var r = await db.HerbRequisitions.Include(x => x.Allocations).SingleOrDefaultAsync(x => x.Id == id, ct);
        if (r == null) return Result(404, "Không tìm thấy phiếu lĩnh.");
        if (r.Status != "DISCREPANCY" || !Version(r.RowVersion, input.RowVersion))
            return Result(409, "Phiếu đã thay đổi. Vui lòng tải lại trước khi xử lý.");
        var outbounds = await db.HerbOutbounds.Where(x => x.RequestId == id).ToListAsync(ct);
        var outboundIds = outbounds.Select(x => x.Id).ToArray();
        var reports = await db.HerbReceiptDiscrepancies.Where(x => outboundIds.Contains(x.OutboundId) && x.Status == "OPEN").ToListAsync(ct);
        if (reports.Count == 0 || r.Allocations.Count == 0 || r.Allocations.Any(x => !x.ReceivedQuantity.HasValue))
            return Result(409, "Không có biên bản mở hoặc phiếu chưa được kiểm nhận đầy đủ.");
        var now = DateTime.UtcNow;
        foreach (var report in reports)
        {
            report.Status = "RESOLVED"; report.Resolution = input.Resolution.Trim();
            report.ResolvedBy = Actor; report.ResolvedAt = now;
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
        return Result(200, "Đã xử lý chênh lệch, chờ Herbalist xác nhận đã nhận đủ.", new { r.Id, r.Status });
    }

    [HttpPost("{id:int}/reject")]
    public async Task<IActionResult> Reject(int id, RejectInput input, CancellationToken ct)
    {
        if (!await Ready(ct)) return Result(503, "The herbalist schema and increased stock precision are required.");
        if (string.IsNullOrWhiteSpace(input.Reason)) return Result(400, "A rejection reason is required.");
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        var r = await db.HerbRequisitions.Include(r => r.Prescriptions).SingleOrDefaultAsync(r => r.Id == id, ct);
        if (r == null) return Result(404, "Requisition was not found.");
        if (r.Status != "REQUESTED" || !Version(r.RowVersion, input.RowVersion)) return Result(409, "Only a requisition awaiting warehouse processing with the current version can be rejected.");
        r.ClosedAt=DateTime.UtcNow; r.ClosedBy=Actor; r.Status = "REJECTED"; r.RejectionReason = input.Reason.Trim();
        foreach (var link in r.Prescriptions) link.IsActive = false;
        await db.SaveChangesAsync(ct); await tx.CommitAsync(ct);
        return Result(200, "Đã từ chối phiếu, các đơn có thể được lập phiếu mới.", new { r.Id, r.Status });
    }
    [HttpGet("{id:int}/fefo")]
    public async Task<IActionResult> Fefo(int id,CancellationToken ct)
    {
        if(!await Ready(ct))return Result(503,"Apply Herbalist_Canonical.sql first.");
        var r=await db.HerbRequisitions.AsNoTracking().SingleOrDefaultAsync(x=>x.Id==id,ct);
        if(r==null)return Result(404,"Request was not found.");
        if(r.Status != "REQUESTED") return Result(409,"The requisition is no longer awaiting warehouse processing.");
        if (await db.HerbRequisitionDetails.AnyAsync(d => d.RequestId == id && d.UnitSnapshot != "g", ct)) return Result(422, "The legacy requisition contains unreconciled units.");
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
        return Result(200,"Gợi ý FEFO; chưa giữ tồn.",suggestions);
    }
}




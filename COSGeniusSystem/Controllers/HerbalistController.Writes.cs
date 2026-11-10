using COSGeniusSystem.Services.Herbalist;
using System.Data;
using COSGeniusSystem.DTOs;
using COSGeniusSystem.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace COSGeniusSystem.Controllers;

public partial class HerbalistController
{
    private record SelectionFailure(int Status, string Message);
    private async Task<SelectionFailure?> SelectionError(int[] ids, CancellationToken ct)
    {
        if (ids.Length == 0 || ids.Distinct().Count() != ids.Length || ids.Any(id => id <= 0)) return new(400, "Chọn từ 1 đến 100 đơn, không trùng mã.");
        if (await Prescriptions().CountAsync(p => ids.Contains(p.PrescriptionId) && p.MedicineDispense == null, ct) != ids.Length) return new(409, "Đơn không tồn tại, chưa gửi hoặc đã được xử lý theo luồng xuất thuốc cũ.");
        if (await db.HerbRequisitionPrescriptions.AnyAsync(l => ids.Contains(l.PrescriptionId) && l.IsActive, ct)) return new(409, "Có đơn đã nằm trong phiếu lĩnh đang xử lý hoặc đã bốc xong. Vui lòng tải lại.");
        var invalidHerbs = await db.HerbalPrescriptionDetails.AsNoTracking()
            .Where(d => ids.Contains(d.PrescriptionId))
            .Select(d => new { d.Herb.HerbCode, d.Herb.CommonName, d.Herb.IsActive, d.Herb.Unit })
            .Distinct().ToListAsync(ct);
        var invalid = invalidHerbs.Where(h => !h.IsActive || !HerbalistWorkflowRules.SupportsStockUnit(h.Unit)).ToList();
        if (invalid.Count > 0) return new(422, "Không thể lĩnh thuốc: " + string.Join("; ", invalid.Select(h =>
            $"{h.HerbCode} ({h.CommonName}): " + (!h.IsActive ? "đã ngừng hoạt động" : $"đơn vị tồn '{h.Unit}' không hỗ trợ, cần g hoặc kg"))) + ".");
        if (await Prescriptions().AnyAsync(p => ids.Contains(p.PrescriptionId) && (p.TotalDoses <= 0 || !p.HerbalPrescriptionDetails.Any() || p.HerbalPrescriptionDetails.Any(d => d.DosagePerDose <= 0)), ct)) return new(422, "Đơn thuốc thiếu thành phần hoặc lượng kê không hợp lệ.");
        return null;
    }

    // Resolve the database ID instead of assuming the only warehouse has ID 1.
    private async Task<(int Id, string? Error)> ResolveWarehouse(int requestedId, CancellationToken ct)
    {
        var ids = await db.Warehouses.AsNoTracking().Where(w => w.IsActive)
            .Select(w => w.WarehouseId).Take(2).ToListAsync(ct);
        if (ids.Count == 0) return (0, "Chưa có kho đang hoạt động để lĩnh thuốc.");
        if (ids.Count > 1) return (0, "Hệ thống chỉ áp dụng một kho. Vui lòng kiểm tra cấu hình kho đang hoạt động.");
        if (requestedId != 0 && requestedId != ids[0]) return (0, "Kho không hợp lệ. Vui lòng tải lại dữ liệu kho.");
        return (ids[0], null);
    }

    [HttpPost("requests/preview")]
    public async Task<IActionResult> Preview(HerbalistSelection input, CancellationToken ct)
    {
        if (!await SchemaReady(ct)) return Failure(503, MissingSchema);
        var warehouse = await ResolveWarehouse(input.WarehouseId, ct);
        if (warehouse.Error != null) return Failure(400, warehouse.Error);
        var error = await SelectionError(input.PrescriptionIds, ct);
        if (error != null) return Failure(error.Status, error.Message);
        var ingredients = await Demand(input.PrescriptionIds, ct, warehouse.Id);
        var perPrescription = await db.HerbalPrescriptionDetails.AsNoTracking().Where(d => input.PrescriptionIds.Contains(d.PrescriptionId)).Select(d => new { d.HerbId, d.PrescriptionId, unit = "g", totalGrams = d.DosagePerDose * d.Prescription.TotalDoses }).ToListAsync(ct);
        return Data(new { prescriptions = await Rows(Prescriptions().Where(p => input.PrescriptionIds.Contains(p.PrescriptionId))).ToListAsync(ct), ingredients, perPrescription, canSubmit = ingredients.Count > 0 && ingredients.All(i => i.ShortageGrams == 0) });
    }

    [HttpPost("requests")]
    public async Task<IActionResult> Create(HerbalistCreateRequest input, CancellationToken ct)
    {
        if (!await SchemaReady(ct)) return Failure(503, MissingSchema);
        // Serializable + filtered unique index stop two users requesting the same prescription.
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        var warehouse = await ResolveWarehouse(input.WarehouseId, ct);
        if (warehouse.Error != null) return Failure(400, warehouse.Error);
        var error = await SelectionError(input.PrescriptionIds, ct);
        if (error != null) return Failure(error.Status, error.Message);
        var demand = await Demand(input.PrescriptionIds, ct, warehouse.Id);
        if (demand.Count == 0 || demand.Any(d => d.ShortageGrams > 0)) return Failure(409, "Thiếu tồn kho khả dụng. Kiểm tra lại nhu cầu trước khi gửi phiếu.");
        var entity = new HerbRequisition
        {
            WarehouseId = warehouse.Id, CreatedBy = Actor, CreatedAt = DateTime.UtcNow, Note = input.Note?.Trim(),
            Prescriptions = input.PrescriptionIds.Select(id => new HerbRequisitionPrescription { PrescriptionId = id }).ToList()
        };
                entity.Details = await db.HerbalPrescriptionDetails.Where(d => input.PrescriptionIds.Contains(d.PrescriptionId))
          .Select(d => new HerbRequisitionDetail { PrescriptionId = d.PrescriptionId, PrescriptionDetailId = d.PrescriptionDetailId, HerbId = d.HerbId, RequestedQuantity = d.DosagePerDose * d.Prescription.TotalDoses, UnitSnapshot = "g" }).ToListAsync(ct);
        db.HerbRequisitions.Add(entity);
        try { await db.SaveChangesAsync(ct); await tx.CommitAsync(ct); }
        catch (DbUpdateException) { return Failure(409, "Đơn đã thay đổi hoặc đã được người khác lập phiếu. Vui lòng tải lại."); }
        return Data(new { entity.Id, code = Code(entity.Id) });
    }

    private static bool Matches(byte[] version, string submitted)
    {
        try { return version.SequenceEqual(Convert.FromBase64String(submitted)); }
        catch (FormatException) { return false; }
    }

    [HttpPost("requests/{id:int}/receive")]
    public async Task<IActionResult> Receive(int id, ReceiveHerbs input, CancellationToken ct)
    {
        if (!await SchemaReady(ct)) return Failure(503, MissingSchema);
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        var r = await db.HerbRequisitions.Include(x=>x.Allocations).SingleOrDefaultAsync(x=>x.Id==id,ct);
        if(r==null) return Failure(404,"Không tìm thấy yêu cầu.");
        if(r.Status!="ISSUED" || !Matches(r.RowVersion,input.RowVersion)) return Failure(409,"Phiếu đã thay đổi hoặc đã nhận.");
        if(input.Lines.Select(x=>x.OutboundDetailId).Distinct().Count()!=input.Lines.Length || input.Lines.Any(x=>!r.Allocations.Any(a=>a.Id==x.OutboundDetailId))) return Failure(400,"Dòng xuất không hợp lệ.");
        var outbound = await db.HerbOutbounds.SingleAsync(x=>x.RequestId==id,ct);
        foreach(var line in input.Lines)
        {
            var a=r.Allocations.Single(x=>x.Id==line.OutboundDetailId);
            if(a.ReceivedQuantity.HasValue) return Failure(409,"Dòng đã kiểm tra không được ghi đè.");
            var quantity=line.ReceiveFull ? a.QuantityGrams : line.ReceivedQuantity;
            if(quantity<0 || (quantity.HasValue && decimal.Round(quantity.Value,2)!=quantity)) return Failure(400,"Lượng nhận phải không âm, tối đa 2 số thập phân gam.");
            a.ReceivedQuantity=quantity;
            if(quantity.HasValue) { a.ReceivedBy=Actor;a.ReceivedAt=DateTime.UtcNow; }
        }
        HerbReceiptDiscrepancy? report=null;
        var differences=r.Allocations.Where(a=>a.ReceivedQuantity.HasValue && a.ReceivedQuantity!=a.QuantityGrams).ToList();
        if(differences.Count>0 && string.IsNullOrWhiteSpace(input.Reason)) return Failure(400,"Cần lý do chênh lệch.");
        // Report once, after every line is checked; partial checks remain visible as NULL.
        if(r.Allocations.All(a=>a.ReceivedQuantity.HasValue))
        {
            outbound.ReceivedAt=r.ReceivedAt=DateTime.UtcNow; outbound.ReceivedBy=r.ReceivedBy=Actor;
            outbound.ReceiptStatus=differences.Count>0?"DISCREPANCY":"MATCHED";
            r.Status=differences.Count>0?"DISCREPANCY":"RECEIVED";
            if(differences.Count>0) {
                report=new HerbReceiptDiscrepancy {
                OutboundId=outbound.Id,ReportedBy=Actor,ReportedAt=DateTime.UtcNow,Content=input.Reason!.Trim(),
                Details=differences.Select(a=>new HerbReceiptDiscrepancyDetail { OutboundId=outbound.Id,OutboundDetailId=a.Id,ExpectedQuantity=a.QuantityGrams,ReceivedQuantity=a.ReceivedQuantity!.Value,Reason=input.Reason.Trim() }).ToList()
                }; db.HerbReceiptDiscrepancies.Add(report);
            }
        }
        // Touch header so its rowversion also protects partial receipt operations.
        r.Note=r.Note; db.Entry(r).Property(x=>x.Note).IsModified=true;
        await db.SaveChangesAsync(ct); await tx.CommitAsync(ct);
        return Data(new {r.Id,r.Status,outboundId=outbound.Id,reportId=report?.Id,rowVersion=Convert.ToBase64String(r.RowVersion)});
    }

    [HttpPost("prescriptions/{id:int}/complete")]
    public async Task<IActionResult> Complete(int id, CompleteHerbs input, CancellationToken ct)
    {
        if (!await SchemaReady(ct)) return Failure(503, MissingSchema);
        await using var tx=await db.Database.BeginTransactionAsync(IsolationLevel.Serializable,ct);
        var link=await db.HerbRequisitionPrescriptions.Include(x=>x.Request).ThenInclude(x=>x.Allocations).SingleOrDefaultAsync(x=>x.PrescriptionId==id && x.IsActive,ct);
        if(link==null) return Failure(404,"Không có yêu cầu đang hiệu lực.");
        var r=link.Request;
        if(link.CompletedAt.HasValue || await db.MedicineDispenses.AnyAsync(x=>x.PrescriptionId==id,ct)) return Failure(409,"Đơn đã bốc xong.");
        if((r.Status!="RECEIVED" && r.Status!="DISCREPANCY") || !Matches(r.RowVersion,input.RowVersion)) return Failure(409,"Chưa đối chiếu xong hoặc phiên bản đã thay đổi.");
        if(input.Allocations.Select(x=>x.OutboundDetailId).Distinct().Count()!=input.Allocations.Length || input.Allocations.Any(x=>x.QuantityGrams<=0 || decimal.Round(x.QuantityGrams,2)!=x.QuantityGrams || !r.Allocations.Any(a=>a.Id==x.OutboundDetailId))) return Failure(400,"Phân bổ không hợp lệ.");
        var needs=await db.HerbRequisitionDetails.Where(x=>x.RequestId==r.Id && x.PrescriptionId==id).GroupBy(x=>x.HerbId).Select(g=>new {HerbId=g.Key,Quantity=g.Sum(x=>x.RequestedQuantity)}).ToDictionaryAsync(x=>x.HerbId,x=>x.Quantity,ct);
        var supplied=input.Allocations.GroupBy(x=>r.Allocations.Single(a=>a.Id==x.OutboundDetailId).HerbId).ToDictionary(g=>g.Key,g=>g.Sum(x=>x.QuantityGrams));
        if(needs.Count!=supplied.Count || needs.Any(x=>supplied.GetValueOrDefault(x.Key)!=x.Value)) return Failure(400,"Phải đủ chính xác nhu cầu từng vị của đơn.");
        var dispense=new MedicineDispense {PrescriptionId=id,WarehouseId=r.WarehouseId,DispensedBy=Actor,DispenseDate=DateTime.UtcNow,Status="COMPLETED",HerbOutboundId=r.Allocations.First().OutboundId};
        foreach(var allocation in input.Allocations)
        {
            var source=r.Allocations.Single(a=>a.Id==allocation.OutboundDetailId);
            // Surplus is quarantined until discrepancy resolution; only verified issued quantity is usable.
            
            var used=await db.DispenseDetails.Where(d=>d.HerbOutboundDetailId==source.Id).SumAsync(d=>(decimal?)d.QuantityTaken,ct)??0m;
            if(!HerbalistWorkflowRules.FitsReceipt(source.QuantityGrams,source.ReceivedQuantity,used,allocation.QuantityGrams)) return Failure(409,"Phân bổ vượt thực nhận hợp lệ còn lại.");
            dispense.DispenseDetails.Add(new DispenseDetail {BatchId=source.BatchId,HerbId=source.HerbId,HerbOutboundDetailId=source.Id,QuantityTaken=allocation.QuantityGrams});
        }
        db.MedicineDispenses.Add(dispense);link.CompletedAt=DateTime.UtcNow;link.CompletedBy=Actor;
        db.Entry(r).Property(x=>x.Note).IsModified=true;
        await db.SaveChangesAsync(ct); await tx.CommitAsync(ct);
        return Data(new {id,dispense.DispenseId,link.CompletedAt});
    }

    [HttpPost("requests/{id:int}/cancel")]
    public async Task<IActionResult> Cancel(int id, HerbalistVersion input, CancellationToken ct)
    {
        await using var tx=await db.Database.BeginTransactionAsync(IsolationLevel.Serializable,ct);
        var r=await db.HerbRequisitions.Include(x=>x.Prescriptions).SingleOrDefaultAsync(x=>x.Id==id,ct);
        if(r==null)return Failure(404,"Không tìm thấy yêu cầu.");
        if(r.Status!="REQUESTED" || !Matches(r.RowVersion,input.RowVersion))return Failure(409,"Chỉ hủy yêu cầu chưa xuất và đúng phiên bản.");
        r.Status="CANCELLED";r.ClosedBy=Actor;r.ClosedAt=DateTime.UtcNow;r.RejectionReason="Herbalist hủy yêu cầu";
        foreach(var l in r.Prescriptions)l.IsActive=false;
        await db.SaveChangesAsync(ct);await tx.CommitAsync(ct);return Data(new {r.Id,r.Status});
    }

    [HttpGet("discrepancies")]
    public async Task<IActionResult> Discrepancies([FromQuery] HerbalistFilter f,CancellationToken ct)
    {
        var q=db.HerbReceiptDiscrepancies.AsNoTracking();
        if(f.Status!=null)q=q.Where(x=>x.Status==f.Status);
        return Data(Page(await q.OrderByDescending(x=>x.Id).Skip((f.PageNumber-1)*f.PageSize).Take(f.PageSize).ToListAsync(ct),await q.CountAsync(ct),f));
    }
    [HttpGet("discrepancies/{id:int}")]
    public async Task<IActionResult> Discrepancy(int id,CancellationToken ct)
    {
        var report=await db.HerbReceiptDiscrepancies.AsNoTracking().Include(x=>x.Details).SingleOrDefaultAsync(x=>x.Id==id,ct);
        return report==null?Failure(404,"Không tìm thấy báo cáo."):Data(report);
    }
    [HttpPost("outbounds/{outboundId:int}/discrepancy-report")]
    public async Task<IActionResult> CreateDiscrepancy(int outboundId,ReceiveHerbs input,CancellationToken ct)
    {
        if (!await SchemaReady(ct)) return Failure(503,MissingSchema);
        var outbound=await db.HerbOutbounds.AsNoTracking().SingleOrDefaultAsync(x=>x.Id==outboundId,ct);
        if(outbound==null)return Failure(404,"Không tìm thấy phiếu xuất.");
        var issued=await db.HerbOutboundDetails.Where(x=>x.OutboundId==outboundId).ToListAsync(ct);
        if(!input.Lines.Any(l=>!l.ReceiveFull && l.ReceivedQuantity.HasValue && issued.Any(a=>a.Id==l.OutboundDetailId && a.QuantityGrams!=l.ReceivedQuantity)) && !issued.Any(a=>a.ReceivedQuantity.HasValue && a.ReceivedQuantity!=a.QuantityGrams))return Failure(400,"Không có dòng chênh lệch.");
        if(issued.Any(a=>!a.ReceivedQuantity.HasValue && !input.Lines.Any(l=>l.OutboundDetailId==a.Id && (l.ReceiveFull || l.ReceivedQuantity.HasValue))))return Failure(400,"Cần đối chiếu hết các dòng để lập báo cáo chung.");
        return await Receive(outbound.RequestId,input,ct);
    }
    [HttpGet("prescriptions/{id:int}/payment-eligibility")]
    public async Task<IActionResult> PaymentEligibility(int id,CancellationToken ct)
    {
        if(!await db.HerbalPrescriptions.AnyAsync(p=>p.PrescriptionId==id,ct))return Failure(404,"Không tìm thấy đơn.");
        return Data(new {prescriptionId=id,eligible=await db.MedicineDispenses.AnyAsync(d=>d.PrescriptionId==id && d.Status=="COMPLETED",ct)});
    }
}




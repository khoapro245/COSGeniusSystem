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
        if (ids.Length == 0 || ids.Length > 100 || ids.Distinct().Count() != ids.Length || ids.Any(id => id <= 0)) return new(400, "Select between 1 and 100 prescriptions with no duplicate identifiers.");
        if (await Prescriptions().CountAsync(p => ids.Contains(p.PrescriptionId) && p.MedicineDispense == null, ct) != ids.Length) return new(409, "A prescription does not exist, has not been submitted, or was processed through the legacy dispensing workflow.");
        if (await db.HerbRequisitionPrescriptions.AnyAsync(l => ids.Contains(l.PrescriptionId) && l.IsActive, ct)) return new(409, "A prescription is already in an active requisition or has been dispensed. Please reload.");
        if (await Rows(Prescriptions().Where(p => ids.Contains(p.PrescriptionId))).AnyAsync(p => !p.Paid, ct))
            return new(409, "??n ch?a thanh to?n, ch?a th? t?o y?u c?u c?p d??c li?u.");
        var invalidHerbs = await db.HerbalPrescriptionDetails.AsNoTracking()
            .Where(d => ids.Contains(d.PrescriptionId))
            .Select(d => new { d.Herb.HerbCode, d.Herb.CommonName, d.Herb.IsActive, d.Herb.Unit })
            .Distinct().ToListAsync(ct);
        var invalid = invalidHerbs.Where(h => !h.IsActive || !HerbalistWorkflowRules.SupportsStockUnit(h.Unit)).ToList();
        if (invalid.Count > 0) return new(422, "Cannot request herbs: " + string.Join("; ", invalid.Select(h =>
            $"{h.HerbCode} ({h.CommonName}): " + (!h.IsActive ? "is inactive" : $"stock unit '{h.Unit}' is unsupported; g or kg is required"))) + ".");
        if (await Prescriptions().AnyAsync(p => ids.Contains(p.PrescriptionId) && (p.TotalDoses <= 0 || !p.HerbalPrescriptionDetails.Any() || p.HerbalPrescriptionDetails.Any(d => d.DosagePerDose <= 0)), ct)) return new(422, "The prescription has missing ingredients or invalid prescribed quantities.");
        return null;
    }

    // Resolve the database ID instead of assuming the only warehouse has ID 1.
    private async Task<(int Id, string? Error)> ResolveWarehouse(int requestedId, CancellationToken ct)
    {
        var ids = await db.Warehouses.AsNoTracking().Where(w => w.IsActive)
            .Select(w => w.WarehouseId).Take(2).ToListAsync(ct);
        if (ids.Count == 0) return (0, "No active warehouse is available for requisition.");
        if (ids.Count > 1) return (0, "The system supports a single warehouse. Please check the active warehouse configuration.");
        if (requestedId != 0 && requestedId != ids[0]) return (0, "Invalid warehouse. Please reload the warehouse data.");
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
        if (demand.Count == 0 || demand.Any(d => d.ShortageGrams > 0)) return Failure(409, "Insufficient available stock. Review the required quantities before submitting.");
        var entity = new HerbRequisition
        {
            WarehouseId = warehouse.Id, CreatedBy = Actor, CreatedAt = DateTime.UtcNow, Note = input.Note?.Trim(),
            Prescriptions = input.PrescriptionIds.Select(id => new HerbRequisitionPrescription { PrescriptionId = id }).ToList()
        };
                entity.Details = await db.HerbalPrescriptionDetails.Where(d => input.PrescriptionIds.Contains(d.PrescriptionId))
          .Select(d => new HerbRequisitionDetail { PrescriptionId = d.PrescriptionId, PrescriptionDetailId = d.PrescriptionDetailId, HerbId = d.HerbId, RequestedQuantity = d.DosagePerDose * d.Prescription.TotalDoses, UnitSnapshot = "g" }).ToListAsync(ct);
        db.HerbRequisitions.Add(entity);
        try
        {
            await db.SaveChangesAsync(ct);
            entity.RequisitionCode = Code(entity.Id);
            await db.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);
        }
        catch (DbUpdateException) { return Failure(409, "A prescription has changed or another user has created its requisition. Please reload."); }
        return Data(new { entity.Id, code = entity.RequisitionCode });
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
        if(r==null) return Failure(404,"Request was not found.");
        if(r.Status!="ISSUED" || !Matches(r.RowVersion,input.RowVersion)) return Failure(409,"The requisition has changed or has already been received.");
        if(input.Lines.Select(x=>x.OutboundDetailId).Distinct().Count()!=input.Lines.Length || input.Lines.Any(x=>!r.Allocations.Any(a=>a.Id==x.OutboundDetailId))) return Failure(400,"Invalid issue line.");
        var outbound = await db.HerbOutbounds.SingleAsync(x=>x.RequestId==id,ct);
        foreach(var line in input.Lines)
        {
            var a=r.Allocations.Single(x=>x.Id==line.OutboundDetailId);
            if(a.ReceivedQuantity.HasValue) return Failure(409,"A checked line cannot be overwritten.");
            var quantity=line.ReceiveFull ? a.QuantityGrams : line.ReceivedQuantity;
            if(quantity<0 || (quantity.HasValue && decimal.Round(quantity.Value,3)!=quantity)) return Failure(400,"The received quantity must be nonnegative with at most 3 decimal places in grams.");
            if (line.ReceiveFull && line.ReceivedQuantity.HasValue) return Failure(400,"Do not submit both full receipt confirmation and an actual received quantity.");
            a.ReceivedQuantity=quantity;
            if(quantity.HasValue) { a.ReceivedBy=Actor;a.ReceivedAt=DateTime.UtcNow; }
        }
        HerbReceiptDiscrepancy? report=null;
        var differences=r.Allocations.Where(a=>a.ReceivedQuantity.HasValue && a.ReceivedQuantity!=a.QuantityGrams).ToList();
        if(differences.Count>0 && string.IsNullOrWhiteSpace(input.Reason)) return Failure(400,"A discrepancy reason is required.");
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
        await db.SaveChangesAsync(ct);
        if (report != null)
        {
            report.ReportCode = $"BC-{report.Id:D6}";
            await db.SaveChangesAsync(ct);
        }
        await tx.CommitAsync(ct);
        return Data(new {r.Id,r.Status,outboundId=outbound.Id,reportId=report?.Id,rowVersion=Convert.ToBase64String(r.RowVersion)});
    }

    [HttpPost("prescriptions/{id:int}/complete")]
    public async Task<IActionResult> Complete(int id, CompleteHerbs input, CancellationToken ct)
    {
        if (!await SchemaReady(ct)) return Failure(503, MissingSchema);
        await using var tx=await db.Database.BeginTransactionAsync(IsolationLevel.Serializable,ct);
        var link=await db.HerbRequisitionPrescriptions.Include(x=>x.Request).ThenInclude(x=>x.Allocations).SingleOrDefaultAsync(x=>x.PrescriptionId==id && x.IsActive,ct);
        if(link==null) return Failure(404,"There is no active request.");
        var r=link.Request;
        if(link.CompletedAt.HasValue || await db.MedicineDispenses.AnyAsync(x=>x.PrescriptionId==id,ct)) return Failure(409,"The prescription has already been dispensed.");
        if(r.Status!="RECEIVED" || !Matches(r.RowVersion,input.RowVersion)) return Failure(409,"Reconciliation is incomplete or the version has changed.");
        if(input.Allocations.Select(x=>x.OutboundDetailId).Distinct().Count()!=input.Allocations.Length || input.Allocations.Any(x=>x.QuantityGrams<=0 || decimal.Round(x.QuantityGrams,3)!=x.QuantityGrams || !r.Allocations.Any(a=>a.Id==x.OutboundDetailId))) return Failure(400,"Invalid allocation.");
        var needs=await db.HerbRequisitionDetails.Where(x=>x.RequestId==r.Id && x.PrescriptionId==id).GroupBy(x=>x.HerbId).Select(g=>new {HerbId=g.Key,Quantity=g.Sum(x=>x.RequestedQuantity)}).ToDictionaryAsync(x=>x.HerbId,x=>x.Quantity,ct);
        var supplied=input.Allocations.GroupBy(x=>r.Allocations.Single(a=>a.Id==x.OutboundDetailId).HerbId).ToDictionary(g=>g.Key,g=>g.Sum(x=>x.QuantityGrams));
        if(needs.Count!=supplied.Count || needs.Any(x=>supplied.GetValueOrDefault(x.Key)!=x.Value)) return Failure(400,"The quantity must exactly match the required amount of each herb in the prescription.");
        var dispense=new MedicineDispense {PrescriptionId=id,WarehouseId=r.WarehouseId,DispensedBy=Actor,DispenseDate=DateTime.UtcNow,Status="COMPLETED",HerbOutboundId=r.Allocations.First().OutboundId};
        foreach(var allocation in input.Allocations)
        {
            var source=r.Allocations.Single(a=>a.Id==allocation.OutboundDetailId);
            // Surplus is quarantined until discrepancy resolution; only verified issued quantity is usable.
            
            var used=await db.DispenseDetails.Where(d=>d.HerbOutboundDetailId==source.Id).SumAsync(d=>(decimal?)d.QuantityTaken,ct)??0m;
            if(!HerbalistWorkflowRules.FitsReceipt(source.QuantityGrams,source.ReceivedQuantity,used,allocation.QuantityGrams)) return Failure(409,"The allocation exceeds the remaining valid received quantity.");
            dispense.DispenseDetails.Add(new DispenseDetail {BatchId=source.BatchId,HerbId=source.HerbId,HerbOutboundDetailId=source.Id,QuantityTaken=allocation.QuantityGrams});
        }
        db.MedicineDispenses.Add(dispense);link.CompletedAt=DateTime.UtcNow;link.CompletedBy=Actor;
        if (!await db.HerbRequisitionPrescriptions.AnyAsync(x => x.RequestId == r.Id && x.PrescriptionId != id && x.CompletedAt == null, ct)) r.Status = "COMPLETED";
        db.Entry(r).Property(x=>x.Note).IsModified=true;
        await db.SaveChangesAsync(ct);
        var recordId = await db.HerbalPrescriptions.Where(p => p.PrescriptionId == id).Select(p => p.RecordId).SingleAsync(ct);
        await COSGeniusSystem.Services.InvoiceAccrual.GenerateAsync(db, recordId, ct);
        await tx.CommitAsync(ct);
        return Data(new {id,dispense.DispenseId,link.CompletedAt});
    }

    [HttpPost("requests/{id:int}/cancel")]
    public async Task<IActionResult> Cancel(int id, HerbalistVersion input, CancellationToken ct)
    {
        await using var tx=await db.Database.BeginTransactionAsync(IsolationLevel.Serializable,ct);
        var r=await db.HerbRequisitions.Include(x=>x.Prescriptions).SingleOrDefaultAsync(x=>x.Id==id,ct);
        if(r==null)return Failure(404,"Request was not found.");
        if(r.Status!="REQUESTED" || !Matches(r.RowVersion,input.RowVersion))return Failure(409,"Only an unissued request with the current version can be cancelled.");
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
        return report==null?Failure(404,"Report was not found."):Data(report);
    }
    [HttpPost("outbounds/{outboundId:int}/discrepancy-report")]
    public async Task<IActionResult> CreateDiscrepancy(int outboundId,ReceiveHerbs input,CancellationToken ct)
    {
        if (!await SchemaReady(ct)) return Failure(503,MissingSchema);
        var outbound=await db.HerbOutbounds.AsNoTracking().SingleOrDefaultAsync(x=>x.Id==outboundId,ct);
        if(outbound==null)return Failure(404,"Issue slip was not found.");
        var issued=await db.HerbOutboundDetails.Where(x=>x.OutboundId==outboundId).ToListAsync(ct);
        if(!input.Lines.Any(l=>!l.ReceiveFull && l.ReceivedQuantity.HasValue && issued.Any(a=>a.Id==l.OutboundDetailId && a.QuantityGrams!=l.ReceivedQuantity)) && !issued.Any(a=>a.ReceivedQuantity.HasValue && a.ReceivedQuantity!=a.QuantityGrams))return Failure(400,"There are no discrepancy lines.");
        if(issued.Any(a=>!a.ReceivedQuantity.HasValue && !input.Lines.Any(l=>l.OutboundDetailId==a.Id && (l.ReceiveFull || l.ReceivedQuantity.HasValue))))return Failure(400,"All lines must be reconciled before creating a consolidated report.");
        return await Receive(outbound.RequestId,input,ct);
    }
    [HttpGet("prescriptions/{id:int}/payment-eligibility")]
    public async Task<IActionResult> PaymentEligibility(int id,CancellationToken ct)
    {
        if(!await db.HerbalPrescriptions.AnyAsync(p=>p.PrescriptionId==id,ct))return Failure(404,"Prescription was not found.");
        return Data(new {prescriptionId=id,eligible=await db.MedicineDispenses.AnyAsync(d=>d.PrescriptionId==id && d.Status=="COMPLETED",ct)});
    }
}

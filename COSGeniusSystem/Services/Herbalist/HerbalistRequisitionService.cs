using System.Data;
using COSGeniusSystem.Data;
using COSGeniusSystem.DTOs;
using COSGeniusSystem.Models;
using Microsoft.EntityFrameworkCore;

namespace COSGeniusSystem.Services.Herbalist;

public enum RequisitionOutcome { Success, Invalid, Conflict, Unprocessable, SchemaUnavailable, NotFound }
public record RequisitionResult(RequisitionOutcome Outcome, string? Message = null, object? Data = null);
public interface IHerbalistRequisitionService
{
    Task<RequisitionResult> CompleteAsync(int id, CompleteHerbs input, int actor, CancellationToken ct);
    Task<RequisitionResult> CancelAsync(int id, HerbalistVersion input, int actor, CancellationToken ct);
    Task<RequisitionResult> CapabilitiesAsync(CancellationToken ct);
    Task<RequisitionResult> ListPrescriptionsAsync(HerbalistFilter f, CancellationToken ct);
    Task<RequisitionResult> PrescriptionAsync(int id, CancellationToken ct);
    Task<RequisitionResult> HistoryAsync(HerbalistFilter f, CancellationToken ct);
    Task<RequisitionResult> LookupsAsync(CancellationToken ct);
    Task<RequisitionResult> RequestsAsync(HerbalistFilter f, CancellationToken ct);
    Task<RequisitionResult> RequestDetailAsync(int id, CancellationToken ct);
    Task<RequisitionResult> PatientsAsync(HerbalistFilter f, CancellationToken ct);
    Task<RequisitionResult> PatientDetailAsync(int id, CancellationToken ct);
    Task<RequisitionResult> HerbsAsync(HerbalistFilter f, CancellationToken ct);
    Task<RequisitionResult> HerbDetailAsync(int id, CancellationToken ct);
    Task<RequisitionResult> FormulasAsync(HerbalistFilter f, CancellationToken ct);
    Task<RequisitionResult> FormulaDetailAsync(int id, CancellationToken ct);
    Task<RequisitionResult> DiscrepanciesAsync(HerbalistFilter f,CancellationToken ct);
    Task<RequisitionResult> DiscrepancyAsync(int id,CancellationToken ct);
    Task<RequisitionResult> PaymentEligibilityAsync(int id,CancellationToken ct);
    Task<RequisitionResult> PreviewAsync(HerbalistSelection input, CancellationToken ct);
    Task<RequisitionResult> CreateAsync(HerbalistCreateRequest input, int actor, CancellationToken ct);
    Task<RequisitionResult> ReceiveAsync(int id, ReceiveHerbs input, int actor, CancellationToken ct);
    Task<RequisitionResult> CreateDiscrepancyAsync(int outboundId, ReceiveHerbs input, int actor, CancellationToken ct);
}
public sealed class HerbalistRequisitionService(ApplicationDbContext db) : IHerbalistRequisitionService
{
    private static RequisitionResult Fail(RequisitionOutcome outcome, string message) => new(outcome, message);
    private static RequisitionResult Success(object data) => new(RequisitionOutcome.Success, Data: data);
    private const string MissingSchema = "The database does not meet the safety requirements for requisition and dispensing. Check readiness for missing components; an administrator must review the schema and scripts/Herbalist_IntegrationGuards.sql.";
    private async Task<List<string>> SchemaIssues(CancellationToken ct) => await db.Database.SqlQueryRaw<string>("""
        SELECT CAST(N'Missing table dbo.' + required.Name AS nvarchar(200)) AS [Value]
        FROM (VALUES (N'HerbRequisition'), (N'HerbRequisitionPrescription'),
            (N'HerbRequisitionDetail'), (N'HerbOutbound'), (N'HerbOutboundDetail'),
            (N'HerbOutboundDemand'), (N'HerbReceiptDiscrepancy'),
            (N'HerbReceiptDiscrepancyDetail'), (N'WarehouseStorageLocation')) required(Name)
        WHERE OBJECT_ID(N'dbo.' + required.Name, N'U') IS NULL
        UNION ALL
        SELECT N'Missing column dbo.HerbOutboundDetail.ReceivedQuantity'
        WHERE COL_LENGTH(N'dbo.HerbOutboundDetail', N'ReceivedQuantity') IS NULL
        UNION ALL
        SELECT N'Missing or disabled trigger dbo.TR_DispenseDetail_ReceiptBudget'
        WHERE NOT EXISTS (SELECT 1 FROM sys.triggers
            WHERE object_id = OBJECT_ID(N'dbo.TR_DispenseDetail_ReceiptBudget', N'TR')
            AND parent_id = OBJECT_ID(N'dbo.DispenseDetail') AND is_disabled = 0)
        UNION ALL
        SELECT N'Batch.CurrentQuantity requires a decimal type with at least 6 decimal places'
        WHERE NOT EXISTS (SELECT 1 FROM sys.columns
            WHERE object_id = OBJECT_ID(N'dbo.Batch') AND name = N'CurrentQuantity'
            AND system_type_id IN (106, 108) AND scale >= 6)
        """).ToListAsync(ct);
    private async Task<bool> SchemaReady(CancellationToken ct) => (await SchemaIssues(ct)).Count == 0;
    private IQueryable<HerbalPrescription> Prescriptions() => db.HerbalPrescriptions.AsNoTracking()
        .Where(p => p.Status == "SENT" && p.Record.Status == "COMPLETED" && p.Record.Registration.Status == "COMPLETED");
    private IQueryable<PrescriptionRow> Rows(IQueryable<HerbalPrescription> q) => q.Select(p => new PrescriptionRow
    {
        Id = p.PrescriptionId, Code = p.PrescriptionCode, PatientId = p.Record.PatientId,
        PatientName = p.Record.Patient.FullName, PatientCode = p.Record.Patient.PatientCode,
        DoctorId = p.Record.DoctorId, DoctorName = p.Record.Doctor.FullName,
        TotalDoses = p.TotalDoses, CreatedAt = p.CreatedAt,
        RequestId = db.HerbRequisitionPrescriptions.Where(l => l.PrescriptionId == p.PrescriptionId && l.IsActive).Select(l => (int?)l.RequestId).FirstOrDefault(),
        RequestStatus = db.HerbRequisitionPrescriptions.Where(l => l.PrescriptionId == p.PrescriptionId && l.IsActive).Select(l => l.Request.Status).FirstOrDefault(),
        CompletedAt = db.HerbRequisitionPrescriptions.Where(l => l.PrescriptionId == p.PrescriptionId && l.IsActive).Select(l => l.CompletedAt).FirstOrDefault(),
        CompletedBy = db.HerbRequisitionPrescriptions.Where(l => l.PrescriptionId == p.PrescriptionId && l.IsActive).Select(l => l.CompletedBy).FirstOrDefault(),
        CompletedByName = db.Users.Where(u => db.HerbRequisitionPrescriptions.Any(l => l.PrescriptionId == p.PrescriptionId && l.IsActive && l.CompletedBy == u.UserId)).Select(u => u.FullName).FirstOrDefault(),
        Paid = db.InvoiceDetails.Any(i => i.PrescriptionId == p.PrescriptionId && i.Invoice.RecordId == p.RecordId && i.ItemType == "HERB_REMEDY" && i.IsActive && (i.Invoice.PaymentStatus == "PAID" || i.Invoice.PaymentStatus == "?? thanh to?n")),
        LegacyDispense = p.MedicineDispense != null
    });
    public class PrescriptionRow
    {
        public int Id { get; set; }
        public string? Code { get; set; }
        public int PatientId { get; set; }
        public string PatientName { get; set; } = "";
        public string? PatientCode { get; set; }
        public int DoctorId { get; set; }
        public string DoctorName { get; set; } = "";
        public int TotalDoses { get; set; }
        public DateTime CreatedAt { get; set; }
        public int? RequestId { get; set; }
        public string? RequestStatus { get; set; }
        public DateTime? CompletedAt { get; set; }
        public int? CompletedBy { get; set; }
        public string? CompletedByName { get; set; }
        public bool Paid { get; set; }
        public bool LegacyDispense { get; set; }
    }

    public record DemandRow(int HerbId, string HerbCode, string Name, decimal TotalGrams, decimal StockKg, decimal ShortageGrams, string Unit = "g");
    private async Task<List<DemandRow>> Demand(int[] ids, CancellationToken ct, int? warehouseId = null)
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow.AddHours(7));
        var needs = await db.HerbalPrescriptionDetails.AsNoTracking().Where(d => ids.Contains(d.PrescriptionId))
            .GroupBy(d => new { d.HerbId, d.Herb.HerbCode, d.Herb.CommonName })
            .Select(g => new { g.Key.HerbId, g.Key.HerbCode, g.Key.CommonName, Grams = g.Sum(d => d.DosagePerDose * d.Prescription.TotalDoses) }).ToListAsync(ct);
        var herbIds = needs.Select(n => n.HerbId).ToArray();
        var stocks = await db.Batches.AsNoTracking().Where(b => herbIds.Contains(b.HerbId) && (!warehouseId.HasValue || b.WarehouseId == warehouseId) && b.StorageLocation != null && b.StorageLocation.IsActive && b.StorageLocation.WarehouseId == b.WarehouseId && b.Herb.IsActive && b.Warehouse.IsActive && b.ExpiryDate >= today && b.CurrentQuantity > 0 && b.Status == "Đạt chuẩn" && (b.Herb.Unit == "kg" || b.Herb.Unit == "g"))
            .GroupBy(b => b.HerbId).Select(g => new { Id = g.Key, Kg = g.Sum(b => Math.Floor((b.Herb.Unit == "kg" ? b.CurrentQuantity * 1000m : b.CurrentQuantity) * 1000m) / 1000000m) }).ToDictionaryAsync(x => x.Id, x => x.Kg, ct);
        return needs.Select(n => new DemandRow(n.HerbId, n.HerbCode, n.CommonName, n.Grams, stocks.GetValueOrDefault(n.HerbId), Math.Max(0, n.Grams - stocks.GetValueOrDefault(n.HerbId) * 1000))).ToList();
    }

    private static string Code(int id) => $"PL-{id:D6}";
    private record SelectionFailure(RequisitionOutcome Outcome, string Message);
    private async Task<SelectionFailure?> SelectionError(int[] ids, CancellationToken ct)
    {
        if (ids.Length == 0 || ids.Length > 100 || ids.Distinct().Count() != ids.Length || ids.Any(id => id <= 0)) return new(RequisitionOutcome.Invalid, "Select between 1 and 100 prescriptions with no duplicate identifiers.");
        if (await Prescriptions().CountAsync(p => ids.Contains(p.PrescriptionId) && p.MedicineDispense == null, ct) != ids.Length) return new(RequisitionOutcome.Conflict, "A prescription does not exist, has not been submitted, or was processed through the legacy dispensing workflow.");
        if (await db.HerbRequisitionPrescriptions.AnyAsync(l => ids.Contains(l.PrescriptionId) && l.IsActive, ct)) return new(RequisitionOutcome.Conflict, "A prescription is already in an active requisition or has been dispensed. Please reload.");
        if (await Rows(Prescriptions().Where(p => ids.Contains(p.PrescriptionId))).AnyAsync(p => !p.Paid, ct))
            return new(RequisitionOutcome.Conflict, "??n ch?a thanh to?n, ch?a th? t?o y?u c?u c?p d??c li?u.");
        var invalidHerbs = await db.HerbalPrescriptionDetails.AsNoTracking()
            .Where(d => ids.Contains(d.PrescriptionId))
            .Select(d => new { d.Herb.HerbCode, d.Herb.CommonName, d.Herb.IsActive, d.Herb.Unit })
            .Distinct().ToListAsync(ct);
        var invalid = invalidHerbs.Where(h => !h.IsActive || !HerbalistWorkflowRules.SupportsStockUnit(h.Unit)).ToList();
        if (invalid.Count > 0) return new(RequisitionOutcome.Unprocessable, "Cannot request herbs: " + string.Join("; ", invalid.Select(h =>
            $"{h.HerbCode} ({h.CommonName}): " + (!h.IsActive ? "is inactive" : $"stock unit '{h.Unit}' is unsupported; g or kg is required"))) + ".");
        if (await Prescriptions().AnyAsync(p => ids.Contains(p.PrescriptionId) && (p.TotalDoses <= 0 || !p.HerbalPrescriptionDetails.Any() || p.HerbalPrescriptionDetails.Any(d => d.DosagePerDose <= 0)), ct)) return new(RequisitionOutcome.Unprocessable, "The prescription has missing ingredients or invalid prescribed quantities.");
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

    public async Task<RequisitionResult> PreviewAsync(HerbalistSelection input, CancellationToken ct)
    {
        if (!await SchemaReady(ct)) return Fail(RequisitionOutcome.SchemaUnavailable, MissingSchema);
        var warehouse = await ResolveWarehouse(input.WarehouseId, ct);
        if (warehouse.Error != null) return Fail(RequisitionOutcome.Invalid, warehouse.Error);
        var error = await SelectionError(input.PrescriptionIds, ct);
        if (error != null) return Fail(error.Outcome, error.Message);
        var ingredients = await Demand(input.PrescriptionIds, ct, warehouse.Id);
        var perPrescription = await db.HerbalPrescriptionDetails.AsNoTracking().Where(d => input.PrescriptionIds.Contains(d.PrescriptionId)).Select(d => new { d.HerbId, d.PrescriptionId, unit = "g", totalGrams = d.DosagePerDose * d.Prescription.TotalDoses }).ToListAsync(ct);
        return Success(new { prescriptions = await Rows(Prescriptions().Where(p => input.PrescriptionIds.Contains(p.PrescriptionId))).ToListAsync(ct), ingredients, perPrescription, canSubmit = ingredients.Count > 0 && ingredients.All(i => i.ShortageGrams == 0) });
    }

    public async Task<RequisitionResult> CreateAsync(HerbalistCreateRequest input, int actor, CancellationToken ct)
    {
        if (!await SchemaReady(ct)) return Fail(RequisitionOutcome.SchemaUnavailable, MissingSchema);
        // Serializable + filtered unique index stop two users requesting the same prescription.
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        var warehouse = await ResolveWarehouse(input.WarehouseId, ct);
        if (warehouse.Error != null) return Fail(RequisitionOutcome.Invalid, warehouse.Error);
        var error = await SelectionError(input.PrescriptionIds, ct);
        if (error != null) return Fail(error.Outcome, error.Message);
        var demand = await Demand(input.PrescriptionIds, ct, warehouse.Id);
        if (demand.Count == 0 || demand.Any(d => d.ShortageGrams > 0)) return Fail(RequisitionOutcome.Conflict, "Insufficient available stock. Review the required quantities before submitting.");
        var entity = new HerbRequisition
        {
            WarehouseId = warehouse.Id, CreatedBy = actor, CreatedAt = DateTime.UtcNow, Note = input.Note?.Trim(),
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
        catch (DbUpdateException) { return Fail(RequisitionOutcome.Conflict, "A prescription has changed or another user has created its requisition. Please reload."); }
        return Success(new { entity.Id, code = entity.RequisitionCode });
    }

    private static bool Matches(byte[] version, string submitted)
    {
        try { return version.SequenceEqual(Convert.FromBase64String(submitted)); }
        catch (FormatException) { return false; }
    }

    public async Task<RequisitionResult> ReceiveAsync(int id, ReceiveHerbs input, int actor, CancellationToken ct)
    {
        if (!await SchemaReady(ct)) return Fail(RequisitionOutcome.SchemaUnavailable, MissingSchema);
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        var r = await db.HerbRequisitions.Include(x=>x.Allocations).SingleOrDefaultAsync(x=>x.Id==id,ct);
        if(r==null) return Fail(RequisitionOutcome.NotFound,"Request was not found.");
        if(r.Status!="ISSUED" || !Matches(r.RowVersion,input.RowVersion)) return Fail(RequisitionOutcome.Conflict,"The requisition has changed or has already been received.");
        if(input.Lines.Select(x=>x.OutboundDetailId).Distinct().Count()!=input.Lines.Length || input.Lines.Any(x=>!r.Allocations.Any(a=>a.Id==x.OutboundDetailId))) return Fail(RequisitionOutcome.Invalid,"Invalid issue line.");
        var outbound = await db.HerbOutbounds.SingleAsync(x=>x.RequestId==id,ct);
        foreach(var line in input.Lines)
        {
            var a=r.Allocations.Single(x=>x.Id==line.OutboundDetailId);
            if(a.ReceivedQuantity.HasValue) return Fail(RequisitionOutcome.Conflict,"A checked line cannot be overwritten.");
            var quantity=line.ReceiveFull ? a.QuantityGrams : line.ReceivedQuantity;
            if(quantity<0 || (quantity.HasValue && decimal.Round(quantity.Value,3)!=quantity)) return Fail(RequisitionOutcome.Invalid,"The received quantity must be nonnegative with at most 3 decimal places in grams.");
            if (line.ReceiveFull && line.ReceivedQuantity.HasValue) return Fail(RequisitionOutcome.Invalid,"Do not submit both full receipt confirmation and an actual received quantity.");
            a.ReceivedQuantity=quantity;
            if(quantity.HasValue) { a.ReceivedBy=actor;a.ReceivedAt=DateTime.UtcNow; }
        }
        HerbReceiptDiscrepancy? report=null;
        var differences=r.Allocations.Where(a=>a.ReceivedQuantity.HasValue && a.ReceivedQuantity!=a.QuantityGrams).ToList();
        if(differences.Count>0 && string.IsNullOrWhiteSpace(input.Reason)) return Fail(RequisitionOutcome.Invalid,"A discrepancy reason is required.");
        // Report once, after every line is checked; partial checks remain visible as NULL.
        if(r.Allocations.All(a=>a.ReceivedQuantity.HasValue))
        {
            outbound.ReceivedAt=r.ReceivedAt=DateTime.UtcNow; outbound.ReceivedBy=r.ReceivedBy=actor;
            outbound.ReceiptStatus=differences.Count>0?"DISCREPANCY":"MATCHED";
            r.Status=differences.Count>0?"DISCREPANCY":"RECEIVED";
            if(differences.Count>0) {
                report=new HerbReceiptDiscrepancy {
                OutboundId=outbound.Id,ReportedBy=actor,ReportedAt=DateTime.UtcNow,Content=input.Reason!.Trim(),
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
        return Success(new {r.Id,r.Status,outboundId=outbound.Id,reportId=report?.Id,rowVersion=Convert.ToBase64String(r.RowVersion)});
    }

    public async Task<RequisitionResult> CreateDiscrepancyAsync(int outboundId, ReceiveHerbs input, int actor, CancellationToken ct)
    {
        if (!await SchemaReady(ct)) return Fail(RequisitionOutcome.SchemaUnavailable,MissingSchema);
        var outbound=await db.HerbOutbounds.AsNoTracking().SingleOrDefaultAsync(x=>x.Id==outboundId,ct);
        if(outbound==null)return Fail(RequisitionOutcome.NotFound,"Issue slip was not found.");
        var issued=await db.HerbOutboundDetails.Where(x=>x.OutboundId==outboundId).ToListAsync(ct);
        if(!input.Lines.Any(l=>!l.ReceiveFull && l.ReceivedQuantity.HasValue && issued.Any(a=>a.Id==l.OutboundDetailId && a.QuantityGrams!=l.ReceivedQuantity)) && !issued.Any(a=>a.ReceivedQuantity.HasValue && a.ReceivedQuantity!=a.QuantityGrams))return Fail(RequisitionOutcome.Invalid,"There are no discrepancy lines.");
        if(issued.Any(a=>!a.ReceivedQuantity.HasValue && !input.Lines.Any(l=>l.OutboundDetailId==a.Id && (l.ReceiveFull || l.ReceivedQuantity.HasValue))))return Fail(RequisitionOutcome.Invalid,"All lines must be reconciled before creating a consolidated report.");
        return await ReceiveAsync(outbound.RequestId,input,actor,ct);
    }
    public async Task<RequisitionResult> CompleteAsync(int id, CompleteHerbs input, int actor, CancellationToken ct)
    {
        if (!await SchemaReady(ct)) return Fail(RequisitionOutcome.SchemaUnavailable, MissingSchema);
        await using var tx=await db.Database.BeginTransactionAsync(IsolationLevel.Serializable,ct);
        var link=await db.HerbRequisitionPrescriptions.Include(x=>x.Request).ThenInclude(x=>x.Allocations).SingleOrDefaultAsync(x=>x.PrescriptionId==id && x.IsActive,ct);
        if(link==null) return Fail(RequisitionOutcome.NotFound,"There is no active request.");
        var r=link.Request;
        if(link.CompletedAt.HasValue || await db.MedicineDispenses.AnyAsync(x=>x.PrescriptionId==id,ct)) return Fail(RequisitionOutcome.Conflict,"The prescription has already been dispensed.");
        if(r.Status!="RECEIVED" || !Matches(r.RowVersion,input.RowVersion)) return Fail(RequisitionOutcome.Conflict,"Reconciliation is incomplete or the version has changed.");
        if(input.Allocations.Select(x=>x.OutboundDetailId).Distinct().Count()!=input.Allocations.Length || input.Allocations.Any(x=>x.QuantityGrams<=0 || decimal.Round(x.QuantityGrams,3)!=x.QuantityGrams || !r.Allocations.Any(a=>a.Id==x.OutboundDetailId))) return Fail(RequisitionOutcome.Invalid,"Invalid allocation.");
        var needs=await db.HerbRequisitionDetails.Where(x=>x.RequestId==r.Id && x.PrescriptionId==id).GroupBy(x=>x.HerbId).Select(g=>new {HerbId=g.Key,Quantity=g.Sum(x=>x.RequestedQuantity)}).ToDictionaryAsync(x=>x.HerbId,x=>x.Quantity,ct);
        var supplied=input.Allocations.GroupBy(x=>r.Allocations.Single(a=>a.Id==x.OutboundDetailId).HerbId).ToDictionary(g=>g.Key,g=>g.Sum(x=>x.QuantityGrams));
        if(needs.Count!=supplied.Count || needs.Any(x=>supplied.GetValueOrDefault(x.Key)!=x.Value)) return Fail(RequisitionOutcome.Invalid,"The quantity must exactly match the required amount of each herb in the prescription.");
        var dispense=new MedicineDispense {PrescriptionId=id,WarehouseId=r.WarehouseId,DispensedBy=actor,DispenseDate=DateTime.UtcNow,Status="COMPLETED",HerbOutboundId=r.Allocations.First().OutboundId};
        foreach(var allocation in input.Allocations)
        {
            var source=r.Allocations.Single(a=>a.Id==allocation.OutboundDetailId);
            // Surplus is quarantined until discrepancy resolution; only verified issued quantity is usable.
            
            var used=await db.DispenseDetails.Where(d=>d.HerbOutboundDetailId==source.Id).SumAsync(d=>(decimal?)d.QuantityTaken,ct)??0m;
            if(!HerbalistWorkflowRules.FitsReceipt(source.QuantityGrams,source.ReceivedQuantity,used,allocation.QuantityGrams)) return Fail(RequisitionOutcome.Conflict,"The allocation exceeds the remaining valid received quantity.");
            dispense.DispenseDetails.Add(new DispenseDetail {BatchId=source.BatchId,HerbId=source.HerbId,HerbOutboundDetailId=source.Id,QuantityTaken=allocation.QuantityGrams});
        }
        db.MedicineDispenses.Add(dispense);link.CompletedAt=DateTime.UtcNow;link.CompletedBy=actor;
        if (!await db.HerbRequisitionPrescriptions.AnyAsync(x => x.RequestId == r.Id && x.PrescriptionId != id && x.CompletedAt == null, ct)) r.Status = "COMPLETED";
        db.Entry(r).Property(x=>x.Note).IsModified=true;
        await db.SaveChangesAsync(ct);
        var recordId = await db.HerbalPrescriptions.Where(p => p.PrescriptionId == id).Select(p => p.RecordId).SingleAsync(ct);
        await COSGeniusSystem.Services.InvoiceAccrual.GenerateAsync(db, recordId, ct);
        await tx.CommitAsync(ct);
        return Success(new {id,dispense.DispenseId,link.CompletedAt});
    }

    public async Task<RequisitionResult> CancelAsync(int id, HerbalistVersion input, int actor, CancellationToken ct)
    {
        await using var tx=await db.Database.BeginTransactionAsync(IsolationLevel.Serializable,ct);
        var r=await db.HerbRequisitions.Include(x=>x.Prescriptions).SingleOrDefaultAsync(x=>x.Id==id,ct);
        if(r==null)return Fail(RequisitionOutcome.NotFound,"Request was not found.");
        if(r.Status!="REQUESTED" || !Matches(r.RowVersion,input.RowVersion))return Fail(RequisitionOutcome.Conflict,"Only an unissued request with the current version can be cancelled.");
        r.Status="CANCELLED";r.ClosedBy=actor;r.ClosedAt=DateTime.UtcNow;r.RejectionReason="Herbalist hủy yêu cầu";
        foreach(var l in r.Prescriptions)l.IsActive=false;
        await db.SaveChangesAsync(ct);await tx.CommitAsync(ct);return Success(new {r.Id,r.Status});
    }

    private static object Page<T>(List<T> items, int count, HerbalistFilter f) => new
    {
        items, totalCount = count, f.PageNumber, f.PageSize,
        totalPages = (int)Math.Ceiling(count / (double)f.PageSize),
        hasPreviousPage = f.PageNumber > 1, hasNextPage = (long)f.PageNumber * f.PageSize < count
    };
    private static void Dates(HerbalistFilter f)
    {
        if (f.FromDate > f.ToDate) throw new COSGeniusSystem.Exceptions.BadRequestException("The start date must be on or before the end date.");
    }
    private IQueryable<HerbalPrescription> Filter(IQueryable<HerbalPrescription> q, HerbalistFilter f)
    {
        Dates(f);
        if (!string.IsNullOrWhiteSpace(f.Search))
        {
            var s = f.Search.Trim();
            q = q.Where(p => (p.PrescriptionCode != null && p.PrescriptionCode.Contains(s)) || p.Record.Patient.FullName.Contains(s) || (p.Record.Patient.PatientCode != null && p.Record.Patient.PatientCode.Contains(s)));
        }
        if (f.DoctorId.HasValue) q = q.Where(p => p.Record.DoctorId == f.DoctorId);
        if (f.PatientId.HasValue) q = q.Where(p => p.Record.PatientId == f.PatientId);
        if (f.FromDate.HasValue) q = q.Where(p => p.CreatedAt >= f.FromDate.Value.Date.AddHours(-7));
        if (f.ToDate.HasValue) q = q.Where(p => p.CreatedAt < f.ToDate.Value.Date.AddDays(1).AddHours(-7));
        return q;
    }
    private async Task<object?> RequestSummary(int id, CancellationToken ct) => await db.HerbRequisitions.AsNoTracking().Where(r => r.Id == id).Select(r => new
    {
        r.Id, r.WarehouseId, r.Status, r.CreatedAt, r.Note, r.ReceivedAt, r.IssuedAt, r.RejectionReason,
        rowVersion = Convert.ToBase64String(r.RowVersion),
        createdByName = db.Users.Where(u => u.UserId == r.CreatedBy).Select(u => u.FullName).FirstOrDefault(),
        receivedByName = db.Users.Where(u => u.UserId == r.ReceivedBy).Select(u => u.FullName).FirstOrDefault(),
        issuedByName = db.Users.Where(u => u.UserId == r.IssuedBy).Select(u => u.FullName).FirstOrDefault(),
        prescriptionCount = r.Prescriptions.Count,
        completedCount = r.Prescriptions.Count(p => p.CompletedAt != null)
    }).SingleOrDefaultAsync(ct);

    public async Task<RequisitionResult> CapabilitiesAsync(CancellationToken ct)
    {
        var issues = await SchemaIssues(ct);
        return Success(new
        {
            workflowReady = issues.Count == 0,
            message = issues.Count == 0 ? null : "Requisition and dispensing are unavailable: " + string.Join("; ", issues) + ". Contact an administrator to check the database configuration.",
            schemaIssues = issues,
            missingCatalogFields = new[] { "Nhóm vị thuốc", "Phiên bản công thức" }
        });
    }

    public async Task<RequisitionResult> ListPrescriptionsAsync(HerbalistFilter f, CancellationToken ct)
    {
        if (!await SchemaReady(ct)) return Fail(RequisitionOutcome.SchemaUnavailable, MissingSchema);
        var all = Rows(Filter(Prescriptions(), f));
        var q = all.Where(p => p.CompletedAt == null && !p.LegacyDispense);
        if (f.Status == "UNLINKED") q = q.Where(p => p.RequestId == null);
        else if (f.Status == "RECEIVED") q = q.Where(p => p.RequestStatus == "RECEIVED");
        else if (f.Status == "REQUESTED" || f.Status == "ISSUED") q = q.Where(p => p.RequestStatus == f.Status);
        var stats = new
        {
            unlinked = await all.CountAsync(p => p.RequestId == null && !p.LegacyDispense, ct),
            waiting = await all.CountAsync(p => p.RequestStatus == "REQUESTED" || p.RequestStatus == "ISSUED", ct),
            received = await all.CountAsync(p => p.RequestStatus == "RECEIVED" && p.CompletedAt == null, ct)
        };
        return Success(new { page = Page(await q.OrderByDescending(p => p.Id).Skip((f.PageNumber - 1) * f.PageSize).Take(f.PageSize).ToListAsync(ct), await q.CountAsync(ct), f), stats });
    }

    public async Task<RequisitionResult> PrescriptionAsync(int id, CancellationToken ct)
    {
        if (!await SchemaReady(ct)) return Fail(RequisitionOutcome.SchemaUnavailable, MissingSchema);
        var row = await Rows(Prescriptions().Where(p => p.PrescriptionId == id)).SingleOrDefaultAsync(ct);
        if (row == null) return Fail(RequisitionOutcome.NotFound, "The submitted prescription was not found.");
        var p = await Prescriptions().Where(p => p.PrescriptionId == id).Select(p => new
        {
            p.Record.RegistrationId, p.PrescriptionName, p.Note, p.UsageInstructions,
            formulaCode = p.SourceHerbalFormula == null ? null : p.SourceHerbalFormula.FormulaCode,
            formulaName = p.SourceHerbalFormula == null ? null : p.SourceHerbalFormula.FormulaName,
            ingredients = p.HerbalPrescriptionDetails.Select(d => new { d.HerbId, d.Herb.HerbCode, name = d.HerbNameSnapshot, d.DosagePerDose, totalGrams = d.DosagePerDose * p.TotalDoses, d.Note }).ToList()
        }).SingleAsync(ct);
        object? request = row.RequestId.HasValue ? await RequestSummary(row.RequestId.Value, ct) : null;
        return Success(new { row, prescription = p, request });
    }

    public async Task<RequisitionResult> HistoryAsync(HerbalistFilter f, CancellationToken ct)
    {
        if (!await SchemaReady(ct)) return Fail(RequisitionOutcome.SchemaUnavailable, MissingSchema);
        // History dates refer to completion, not prescription creation.
        var from = f.FromDate; var to = f.ToDate; Dates(f); f.FromDate = null; f.ToDate = null;
        var q = Rows(Filter(Prescriptions(), f)).Where(p => p.CompletedAt != null);
        if (from.HasValue) q = q.Where(p => p.CompletedAt >= from.Value.Date.AddHours(-7));
        if (to.HasValue) q = q.Where(p => p.CompletedAt < to.Value.Date.AddDays(1).AddHours(-7));
        if (f.CompletedBy.HasValue) q = q.Where(p => p.CompletedBy == f.CompletedBy);
        return Success(Page(await q.OrderByDescending(p => p.CompletedAt).ThenByDescending(p => p.Id).Skip((f.PageNumber - 1) * f.PageSize).Take(f.PageSize).ToListAsync(ct), await q.CountAsync(ct), f));
    }

    public async Task<RequisitionResult> LookupsAsync(CancellationToken ct)
    {
        if (!await SchemaReady(ct)) return Fail(RequisitionOutcome.SchemaUnavailable, MissingSchema);
        return Success(new
        {
            warehouses = await db.Warehouses.AsNoTracking().Where(w=>w.IsActive).Select(w=>new {w.WarehouseId,w.WarehouseName}).ToListAsync(ct),
            doctors = await db.Doctors.AsNoTracking().Select(x => new { id = x.DoctorId, name = x.FullName }).ToListAsync(ct),
            herbalists = await db.Users.AsNoTracking().Where(u => db.HerbRequisitionPrescriptions.Any(l => l.CompletedBy == u.UserId)).Select(u => new { id = u.UserId, name = u.FullName }).ToListAsync(ct)
        });
    }

    public async Task<RequisitionResult> RequestsAsync(HerbalistFilter f, CancellationToken ct)
    {
        if (!await SchemaReady(ct)) return Fail(RequisitionOutcome.SchemaUnavailable, MissingSchema);
        Dates(f);
        var q = db.HerbRequisitions.AsNoTracking().AsQueryable();
        if (!string.IsNullOrEmpty(f.Status)) q = q.Where(r => r.Status == f.Status);
        if (f.FromDate.HasValue) q = q.Where(r => r.CreatedAt >= f.FromDate.Value.Date.AddHours(-7));
        if (f.ToDate.HasValue) q = q.Where(r => r.CreatedAt < f.ToDate.Value.Date.AddDays(1).AddHours(-7));
        if (!string.IsNullOrWhiteSpace(f.Search))
        {
            var s = f.Search.Trim(); var requestId = int.TryParse(s.StartsWith("PL", StringComparison.OrdinalIgnoreCase) ? s[2..] : s, out var parsed) ? parsed : -1;
            q = q.Where(r => r.Id == requestId || r.Prescriptions.Any(l => (l.Prescription.PrescriptionCode != null && l.Prescription.PrescriptionCode.Contains(s)) || l.Prescription.Record.Patient.FullName.Contains(s) || (l.Prescription.Record.Patient.PatientCode != null && l.Prescription.Record.Patient.PatientCode.Contains(s))));
        }
        var count = await q.CountAsync(ct);
        var rows = await q.OrderByDescending(r => r.Id).Skip((f.PageNumber - 1) * f.PageSize).Take(f.PageSize).Select(r => new
        {
            r.Id, r.CreatedAt, r.Status,
            createdByName = db.Users.Where(u => u.UserId == r.CreatedBy).Select(u => u.FullName).FirstOrDefault(),
            prescriptionCount = r.Prescriptions.Count,
            totalGrams = r.Details.Sum(d => (decimal?)d.RequestedQuantity) ?? 0m
        }).ToListAsync(ct);
        return Success(Page(rows, count, f));
    }

    public async Task<RequisitionResult> RequestDetailAsync(int id, CancellationToken ct)
    {
        if (!await SchemaReady(ct)) return Fail(RequisitionOutcome.SchemaUnavailable, MissingSchema);
        var summary = await RequestSummary(id, ct);
        if (summary == null) return Fail(RequisitionOutcome.NotFound, "Requisition was not found.");
        var ids = await db.HerbRequisitionPrescriptions.Where(l => l.RequestId == id).Select(l => l.PrescriptionId).ToArrayAsync(ct);
        // A herb shared by several prescriptions is one receipt row, with its demand summed.
        var ingredients = await (from detail in db.HerbRequisitionDetails.AsNoTracking()
                                 join herb in db.Herbs.AsNoTracking() on detail.HerbId equals herb.HerbId
                                 where detail.RequestId == id
                                 group detail by new { detail.HerbId, herb.HerbCode, herb.CommonName } into g
                                 orderby g.Key.HerbCode
                                 select new { g.Key.HerbId, g.Key.HerbCode, name = g.Key.CommonName,
                                     totalGrams = g.Sum(d => d.RequestedQuantity) }).ToListAsync(ct);
        var allocations = await db.HerbOutboundDetails.AsNoTracking().Where(a => a.RequestId == id).Select(a => new { a.Id, a.BatchId, a.Batch.HerbId, a.Batch.BatchNumber, a.Batch.ExpiryDate, a.WarehouseId, a.QuantityGrams, a.OutboundId, a.ReceivedQuantity, a.ReceivedBy, a.ReceivedAt, a.LocationSnapshot, a.StorageLocationId }).ToListAsync(ct);
        var rows = await Rows(Prescriptions().Where(p => ids.Contains(p.PrescriptionId))).ToListAsync(ct);
        var links = await db.HerbRequisitionPrescriptions.AsNoTracking().Where(l => l.RequestId == id).ToDictionaryAsync(l => l.PrescriptionId, ct);
        var requestStatus = await db.HerbRequisitions.Where(r => r.Id == id).Select(r => r.Status).SingleAsync(ct);
        // A rejected request can have prescriptions linked to newer requests. Keep this view historical.
        foreach (var row in rows)
        {
            row.RequestId = id; row.RequestStatus = requestStatus;
            row.CompletedAt = links[row.Id].CompletedAt; row.CompletedBy = links[row.Id].CompletedBy;
        }
                var outboundDemand=await db.Set<HerbOutboundDemand>().AsNoTracking().Where(x=>x.OutboundDetail.RequestId==id).Select(x=>new {x.OutboundDetailId,x.RequisitionDetailId,x.QuantityGrams}).ToListAsync(ct);
        var dispenseDetails=await db.DispenseDetails.AsNoTracking().Where(x=>ids.Contains(x.Dispense.PrescriptionId)).Select(x=>new {x.Dispense.PrescriptionId,x.HerbOutboundDetailId,x.BatchId,x.HerbId,x.QuantityTaken}).ToListAsync(ct);
        return Success(new { summary, prescriptions = rows, ingredients, allocations, outboundDemand, dispenseDetails });
    }

    public async Task<RequisitionResult> PatientsAsync(HerbalistFilter f, CancellationToken ct)
    {
        var q = db.Patients.AsNoTracking().AsQueryable();
        if (!string.IsNullOrWhiteSpace(f.Search))
        {
            var s = f.Search.Trim();
            q = q.Where(p => p.FullName.Contains(s) || (p.PatientCode != null && p.PatientCode.Contains(s)) || (p.Phone != null && p.Phone.Contains(s)));
        }
        var count = await q.CountAsync(ct);
        var rows = await q.OrderByDescending(p => p.PatientId).Skip((f.PageNumber - 1) * f.PageSize).Take(f.PageSize)
            .Select(p => new { id = p.PatientId, p.PatientCode, p.FullName, p.DateOfBirth, p.Gender, p.Phone }).ToListAsync(ct);
        return Success(Page(rows, count, f));
    }

    public async Task<RequisitionResult> PatientDetailAsync(int id, CancellationToken ct)
    {
        var patient = await db.Patients.AsNoTracking().Where(p => p.PatientId == id).Select(p => new
        {
            id = p.PatientId, p.PatientCode, p.FullName, p.DateOfBirth, p.Gender, p.Phone, p.Address, p.AllergyNote,
            contacts = p.PatientContacts.Select(c => new { c.FullName, c.Phone }).ToList()
        }).SingleOrDefaultAsync(ct);
        return patient == null ? Fail(RequisitionOutcome.NotFound, "Patient was not found.") : Success(patient);
    }

    public async Task<RequisitionResult> HerbsAsync(HerbalistFilter f, CancellationToken ct)
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow.AddHours(7));
        var q = db.Herbs.AsNoTracking().AsQueryable();
        if (!string.IsNullOrWhiteSpace(f.Search)) q = q.Where(h => h.CommonName.Contains(f.Search) || h.HerbCode.Contains(f.Search));
        if (f.Status == "ACTIVE") q = q.Where(h => h.IsActive);
        if (f.Status == "INACTIVE") q = q.Where(h => !h.IsActive);
        var count = await q.CountAsync(ct);
        var rows = await q.OrderBy(h => h.CommonName).ThenBy(h => h.HerbId).Skip((f.PageNumber - 1) * f.PageSize).Take(f.PageSize).Select(h => new
        {
            id = h.HerbId, h.HerbCode, h.CommonName, h.ScientificName, h.Unit, h.IsActive,
            stock = h.Batches.Where(b => b.CurrentQuantity > 0 && b.ExpiryDate >= today && b.Status == "Đạt chuẩn" && b.Warehouse.IsActive).Sum(b => b.CurrentQuantity)
        }).ToListAsync(ct);
        return Success(Page(rows, count, f));
    }

    public async Task<RequisitionResult> HerbDetailAsync(int id, CancellationToken ct)
    {
        var herb = await db.Herbs.AsNoTracking().Where(h => h.HerbId == id).Select(h => new { id = h.HerbId, h.HerbCode, h.CommonName, h.ScientificName, h.Unit, h.IsActive, h.TasteProperties }).SingleOrDefaultAsync(ct);
        if (herb == null) return Fail(RequisitionOutcome.NotFound, "Herb was not found.");
        var today = DateOnly.FromDateTime(DateTime.UtcNow.AddHours(7));
        var batches = await db.Batches.AsNoTracking().Where(b => b.HerbId == id).OrderBy(b => b.ExpiryDate).ThenBy(b => b.BatchId).Select(b => new
        {
            id = b.BatchId, b.BatchNumber, b.ExpiryDate, b.CurrentQuantity, b.Status,
            b.Warehouse.WarehouseName, warehouseAddress = b.Warehouse.Location, b.StorageLocationId, location = b.StorageLocation == null ? null : b.StorageLocation.Code,
            suppliers = b.ReceiptDetails.Where(d => d.Receipt.Po != null).Select(d => d.Receipt.Po!.Supplier.SupplierName).Distinct().ToList(),
            available = b.CurrentQuantity > 0 && b.ExpiryDate >= today && b.Status == "Đạt chuẩn" && b.Warehouse.IsActive
        }).ToListAsync(ct);
        return Success(new { herb, batches });
    }

    public async Task<RequisitionResult> FormulasAsync(HerbalistFilter f, CancellationToken ct)
    {
        var q = db.HerbalFormulas.AsNoTracking().Where(h => h.Status == "APPROVED" && h.IsActive);
        if (!string.IsNullOrWhiteSpace(f.Search)) q = q.Where(h => h.FormulaName.Contains(f.Search) || (h.FormulaCode != null && h.FormulaCode.Contains(f.Search)));
        var count = await q.CountAsync(ct);
        return Success(Page(await q.OrderBy(h => h.FormulaName).ThenBy(h => h.HerbalFormulaId).Skip((f.PageNumber - 1) * f.PageSize).Take(f.PageSize).Select(h => new
        {
            id = h.HerbalFormulaId, h.FormulaCode, h.FormulaName, h.ReviewedAt, h.Status
        }).ToListAsync(ct), count, f));
    }

    public async Task<RequisitionResult> FormulaDetailAsync(int id, CancellationToken ct)
    {
        var result = await db.HerbalFormulas.AsNoTracking().Where(h => h.HerbalFormulaId == id && h.Status == "APPROVED" && h.IsActive).Select(h => new
        {
            id = h.HerbalFormulaId, h.FormulaCode, h.FormulaName, h.ReviewedAt, h.IntendedUse, h.UsageInstructions,
            ingredients = h.HerbalFormulaDetails.Select(d => new { d.HerbId, d.Herb.HerbCode, name = d.Herb.CommonName, d.DosagePerDose, d.RoleInFormula, d.Note }).ToList()
        }).SingleOrDefaultAsync(ct);
        return result == null ? Fail(RequisitionOutcome.NotFound, "No approved active formula was found.") : Success(result);
    }

    public async Task<RequisitionResult> DiscrepanciesAsync(HerbalistFilter f,CancellationToken ct)
    {
        var q=db.HerbReceiptDiscrepancies.AsNoTracking();
        if(f.Status!=null)q=q.Where(x=>x.Status==f.Status);
        return Success(Page(await q.OrderByDescending(x=>x.Id).Skip((f.PageNumber-1)*f.PageSize).Take(f.PageSize).ToListAsync(ct),await q.CountAsync(ct),f));
    }

    public async Task<RequisitionResult> DiscrepancyAsync(int id,CancellationToken ct)
    {
        var report=await db.HerbReceiptDiscrepancies.AsNoTracking().Include(x=>x.Details).SingleOrDefaultAsync(x=>x.Id==id,ct);
        return report==null?Fail(RequisitionOutcome.NotFound,"Report was not found."):Success(report);
    }

    public async Task<RequisitionResult> PaymentEligibilityAsync(int id,CancellationToken ct)
    {
        if(!await db.HerbalPrescriptions.AnyAsync(p=>p.PrescriptionId==id,ct))return Fail(RequisitionOutcome.NotFound,"Prescription was not found.");
        return Success(new {prescriptionId=id,eligible=await db.MedicineDispenses.AnyAsync(d=>d.PrescriptionId==id && d.Status=="COMPLETED",ct)});
    }
}

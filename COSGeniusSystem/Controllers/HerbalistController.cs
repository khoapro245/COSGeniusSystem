using System.Security.Claims;
using COSGeniusSystem.Commons;
using COSGeniusSystem.Data;
using COSGeniusSystem.DTOs;
using COSGeniusSystem.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace COSGeniusSystem.Controllers;

[ApiController]
[Route("api/v1/herbalist")]
[Authorize(Roles = "ADMIN,PHARMACIST,HERBALIST")]
public partial class HerbalistController(ApplicationDbContext db) : ControllerBase
{
    private const string MissingSchema = "Database chưa có đủ bảng HerbRequisition, HerbRequisitionPrescription, HerbOutboundDetail. Cần áp dụng script Herbalist_Canonical.sql trước khi sử dụng lĩnh và bốc thuốc.";
    private IActionResult Data(object? value) => Ok(new ApiResponseModel<object>(200, "Thành công.", true, value));
    private IActionResult Failure(int status, string message) => StatusCode(status, new ApiResponseModel<object>(status, message, false, null));
    private async Task<bool> SchemaReady(CancellationToken ct) => await db.Database.SqlQueryRaw<int>(
        "SELECT CASE WHEN OBJECT_ID(N'dbo.HerbRequisition', N'U') IS NOT NULL AND OBJECT_ID(N'dbo.HerbRequisitionPrescription', N'U') IS NOT NULL AND OBJECT_ID(N'dbo.HerbOutboundDemand', N'U') IS NOT NULL AND COL_LENGTH(N'dbo.HerbOutboundDetail',N'ReceivedQuantity') IS NOT NULL THEN 1 ELSE 0 END AS [Value]").SingleAsync(ct) == 1;
    private int Actor => int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier) ?? throw new InvalidOperationException("Missing user identity."));
    private static string Code(int id) => $"PL{id:D5}";
    private static object Page<T>(List<T> items, int count, HerbalistFilter f) => new
    {
        items, totalCount = count, f.PageNumber, f.PageSize,
        totalPages = (int)Math.Ceiling(count / (double)f.PageSize),
        hasPreviousPage = f.PageNumber > 1, hasNextPage = (long)f.PageNumber * f.PageSize < count
    };
    private static void Dates(HerbalistFilter f)
    {
        if (f.FromDate > f.ToDate) throw new COSGeniusSystem.Exceptions.BadRequestException("Từ ngày phải trước hoặc bằng đến ngày.");
    }
    private IQueryable<HerbalPrescription> Prescriptions() => db.HerbalPrescriptions.AsNoTracking()
        .Where(p => p.Status == "SENT" && p.Record.Status == "COMPLETED" && p.Record.Registration.Status == "COMPLETED");
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
        public bool LegacyDispense { get; set; }
    }

    [HttpGet("capabilities")]
    public async Task<IActionResult> Capabilities(CancellationToken ct) => Data(new
    {
        workflowReady = await SchemaReady(ct), message = await SchemaReady(ct) ? null : MissingSchema,
        missingCatalogFields = new[] { "Nhóm vị thuốc", "Phiên bản công thức" }
    });

    [HttpGet("prescriptions")]
    public async Task<IActionResult> ListPrescriptions([FromQuery] HerbalistFilter f, CancellationToken ct)
    {
        if (!await SchemaReady(ct)) return Failure(503, MissingSchema);
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
        return Data(new { page = Page(await q.OrderByDescending(p => p.Id).Skip((f.PageNumber - 1) * f.PageSize).Take(f.PageSize).ToListAsync(ct), await q.CountAsync(ct), f), stats });
    }

    [HttpGet("prescriptions/{id:int}")]
    public async Task<IActionResult> Prescription(int id, CancellationToken ct)
    {
        if (!await SchemaReady(ct)) return Failure(503, MissingSchema);
        var row = await Rows(Prescriptions().Where(p => p.PrescriptionId == id)).SingleOrDefaultAsync(ct);
        if (row == null) return Failure(404, "Không tìm thấy đơn đã gửi.");
        var p = await Prescriptions().Where(p => p.PrescriptionId == id).Select(p => new
        {
            p.Record.RegistrationId, p.PrescriptionName, p.Note, p.UsageInstructions,
            formulaCode = p.SourceHerbalFormula == null ? null : p.SourceHerbalFormula.FormulaCode,
            formulaName = p.SourceHerbalFormula == null ? null : p.SourceHerbalFormula.FormulaName,
            ingredients = p.HerbalPrescriptionDetails.Select(d => new { d.HerbId, d.Herb.HerbCode, name = d.HerbNameSnapshot, d.DosagePerDose, totalGrams = d.DosagePerDose * p.TotalDoses, d.Note }).ToList()
        }).SingleAsync(ct);
        object? request = row.RequestId.HasValue ? await RequestSummary(row.RequestId.Value, ct) : null;
        return Data(new { row, prescription = p, request });
    }

    [HttpGet("history")]
    public async Task<IActionResult> History([FromQuery] HerbalistFilter f, CancellationToken ct)
    {
        if (!await SchemaReady(ct)) return Failure(503, MissingSchema);
        // History dates refer to completion, not prescription creation.
        var from = f.FromDate; var to = f.ToDate; Dates(f); f.FromDate = null; f.ToDate = null;
        var q = Rows(Filter(Prescriptions(), f)).Where(p => p.CompletedAt != null);
        if (from.HasValue) q = q.Where(p => p.CompletedAt >= from.Value.Date.AddHours(-7));
        if (to.HasValue) q = q.Where(p => p.CompletedAt < to.Value.Date.AddDays(1).AddHours(-7));
        if (f.CompletedBy.HasValue) q = q.Where(p => p.CompletedBy == f.CompletedBy);
        return Data(Page(await q.OrderByDescending(p => p.CompletedAt).ThenByDescending(p => p.Id).Skip((f.PageNumber - 1) * f.PageSize).Take(f.PageSize).ToListAsync(ct), await q.CountAsync(ct), f));
    }

    [HttpGet("lookups")]
    public async Task<IActionResult> Lookups(CancellationToken ct)
    {
        if (!await SchemaReady(ct)) return Failure(503, MissingSchema);
        return Data(new
        {
            warehouses = await db.Warehouses.AsNoTracking().Where(w=>w.IsActive).Select(w=>new {w.WarehouseId,w.WarehouseName}).ToListAsync(ct),
            doctors = await db.Doctors.AsNoTracking().Select(x => new { id = x.DoctorId, name = x.FullName }).ToListAsync(ct),
            herbalists = await db.Users.AsNoTracking().Where(u => db.HerbRequisitionPrescriptions.Any(l => l.CompletedBy == u.UserId)).Select(u => new { id = u.UserId, name = u.FullName }).ToListAsync(ct)
        });
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

    [HttpGet("requests")]
    public async Task<IActionResult> Requests([FromQuery] HerbalistFilter f, CancellationToken ct)
    {
        if (!await SchemaReady(ct)) return Failure(503, MissingSchema);
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
        return Data(Page(rows, count, f));
    }

    [HttpGet("requests/{id:int}")]
    public async Task<IActionResult> RequestDetail(int id, CancellationToken ct)
    {
        if (!await SchemaReady(ct)) return Failure(503, MissingSchema);
        var summary = await RequestSummary(id, ct);
        if (summary == null) return Failure(404, "Không tìm thấy phiếu lĩnh.");
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
        return Data(new { summary, prescriptions = rows, ingredients, allocations, outboundDemand, dispenseDetails });
    }

    public record DemandRow(int HerbId, string HerbCode, string Name, decimal TotalGrams, decimal StockKg, decimal ShortageGrams, string Unit = "g");
    private async Task<List<DemandRow>> Demand(int[] ids, CancellationToken ct, int? warehouseId = null)
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow.AddHours(7));
        var needs = await db.HerbalPrescriptionDetails.AsNoTracking().Where(d => ids.Contains(d.PrescriptionId))
            .GroupBy(d => new { d.HerbId, d.Herb.HerbCode, d.Herb.CommonName })
            .Select(g => new { g.Key.HerbId, g.Key.HerbCode, g.Key.CommonName, Grams = g.Sum(d => d.DosagePerDose * d.Prescription.TotalDoses) }).ToListAsync(ct);
        var herbIds = needs.Select(n => n.HerbId).ToArray();
        var stocks = await db.Batches.AsNoTracking().Where(b => herbIds.Contains(b.HerbId) && (!warehouseId.HasValue || b.WarehouseId == warehouseId) && b.StorageLocation != null && b.StorageLocation.IsActive && b.Herb.IsActive && b.Warehouse.IsActive && b.ExpiryDate >= today && b.CurrentQuantity > 0 && b.Status == "Đạt chuẩn" && (b.Herb.Unit == "kg" || b.Herb.Unit == "g"))
            .GroupBy(b => b.HerbId).Select(g => new { Id = g.Key, Kg = g.Sum(b => b.Herb.Unit == "kg" ? b.CurrentQuantity : b.CurrentQuantity / 1000m) }).ToDictionaryAsync(x => x.Id, x => x.Kg, ct);
        return needs.Select(n => new DemandRow(n.HerbId, n.HerbCode, n.CommonName, n.Grams, stocks.GetValueOrDefault(n.HerbId), Math.Max(0, n.Grams - stocks.GetValueOrDefault(n.HerbId) * 1000))).ToList();
    }
}









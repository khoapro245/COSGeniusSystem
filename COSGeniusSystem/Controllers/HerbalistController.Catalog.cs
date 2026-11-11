using COSGeniusSystem.DTOs;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace COSGeniusSystem.Controllers;

public partial class HerbalistController
{
    [HttpGet("patients")]
    public async Task<IActionResult> Patients([FromQuery] HerbalistFilter f, CancellationToken ct)
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
        return Data(Page(rows, count, f));
    }

    [HttpGet("patients/{id:int}")]
    public async Task<IActionResult> PatientDetail(int id, CancellationToken ct)
    {
        var patient = await db.Patients.AsNoTracking().Where(p => p.PatientId == id).Select(p => new
        {
            id = p.PatientId, p.PatientCode, p.FullName, p.DateOfBirth, p.Gender, p.Phone, p.Address, p.AllergyNote,
            contacts = p.PatientContacts.Select(c => new { c.FullName, c.Phone }).ToList()
        }).SingleOrDefaultAsync(ct);
        return patient == null ? Failure(404, "Không tìm thấy bệnh nhân.") : Data(patient);
    }

    [HttpGet("herbs")]
    public async Task<IActionResult> Herbs([FromQuery] HerbalistFilter f, CancellationToken ct)
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
        return Data(Page(rows, count, f));
    }

    [HttpGet("herbs/{id:int}")]
    public async Task<IActionResult> HerbDetail(int id, CancellationToken ct)
    {
        var herb = await db.Herbs.AsNoTracking().Where(h => h.HerbId == id).Select(h => new { id = h.HerbId, h.HerbCode, h.CommonName, h.ScientificName, h.Unit, h.IsActive, h.TasteProperties }).SingleOrDefaultAsync(ct);
        if (herb == null) return Failure(404, "Không tìm thấy vị thuốc.");
        var today = DateOnly.FromDateTime(DateTime.UtcNow.AddHours(7));
        var batches = await db.Batches.AsNoTracking().Where(b => b.HerbId == id).OrderBy(b => b.ExpiryDate).ThenBy(b => b.BatchId).Select(b => new
        {
            id = b.BatchId, b.BatchNumber, b.ExpiryDate, b.CurrentQuantity, b.Status,
            b.Warehouse.WarehouseName, warehouseAddress = b.Warehouse.Location, b.StorageLocationId, location = b.StorageLocation == null ? null : b.StorageLocation.Code,
            suppliers = b.ReceiptDetails.Where(d => d.Receipt.Po != null).Select(d => d.Receipt.Po!.Supplier.SupplierName).Distinct().ToList(),
            available = b.CurrentQuantity > 0 && b.ExpiryDate >= today && b.Status == "Đạt chuẩn" && b.Warehouse.IsActive
        }).ToListAsync(ct);
        return Data(new { herb, batches });
    }

    [HttpGet("formulas")]
    public async Task<IActionResult> Formulas([FromQuery] HerbalistFilter f, CancellationToken ct)
    {
        var q = db.HerbalFormulas.AsNoTracking().Where(h => h.Status == "APPROVED" && h.IsActive);
        if (!string.IsNullOrWhiteSpace(f.Search)) q = q.Where(h => h.FormulaName.Contains(f.Search) || (h.FormulaCode != null && h.FormulaCode.Contains(f.Search)));
        var count = await q.CountAsync(ct);
        return Data(Page(await q.OrderBy(h => h.FormulaName).ThenBy(h => h.HerbalFormulaId).Skip((f.PageNumber - 1) * f.PageSize).Take(f.PageSize).Select(h => new
        {
            id = h.HerbalFormulaId, h.FormulaCode, h.FormulaName, h.ReviewedAt, h.Status
        }).ToListAsync(ct), count, f));
    }

    [HttpGet("formulas/{id:int}")]
    public async Task<IActionResult> FormulaDetail(int id, CancellationToken ct)
    {
        var result = await db.HerbalFormulas.AsNoTracking().Where(h => h.HerbalFormulaId == id && h.Status == "APPROVED" && h.IsActive).Select(h => new
        {
            id = h.HerbalFormulaId, h.FormulaCode, h.FormulaName, h.ReviewedAt, h.IntendedUse, h.UsageInstructions,
            ingredients = h.HerbalFormulaDetails.Select(d => new { d.HerbId, d.Herb.HerbCode, name = d.Herb.CommonName, d.DosagePerDose, d.RoleInFormula, d.Note }).ToList()
        }).SingleOrDefaultAsync(ct);
        return result == null ? Failure(404, "Không tìm thấy công thức đang hoạt động đã duyệt.") : Data(result);
    }
}


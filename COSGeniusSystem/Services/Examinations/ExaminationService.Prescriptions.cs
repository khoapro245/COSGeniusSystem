using System.Security.Claims;
using COSGeniusSystem.Commons;
using COSGeniusSystem.DTOs.Examinations;
using COSGeniusSystem.Exceptions;
using COSGeniusSystem.Models;
using Microsoft.EntityFrameworkCore;

namespace COSGeniusSystem.Services.Examinations;

public sealed partial class ExaminationService
{
    public async Task<PagedResult<FormulaResponse>> FormulasAsync(PageQuery query, CancellationToken ct)
    {
        Validate(query);
        var q = db.HerbalFormulas.AsNoTracking().Where(f => f.Status == "APPROVED" && f.IsActive);
        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var search = query.Search.Trim();
            q = q.Where(f => f.FormulaName.Contains(search) || (f.FormulaCode != null && f.FormulaCode.Contains(search)));
        }
        var total = await q.CountAsync(ct);
        var items = await q.OrderBy(f => f.FormulaName).ThenBy(f => f.HerbalFormulaId)
            .Skip((query.PageNumber - 1) * query.PageSize).Take(query.PageSize)
            .Select(f => new FormulaResponse(f.HerbalFormulaId, f.FormulaCode, f.FormulaName, f.IntendedUse, f.UsageInstructions,
                f.HerbalFormulaDetails.OrderBy(d => d.HerbalFormulaDetailId)
                    .Select(d => new FormulaItemResponse(d.HerbId, d.Herb.CommonName, d.DosagePerDose, d.RoleInFormula, d.Note, d.Herb.IsActive)).ToList()))
            .ToListAsync(ct);
        return new(items, query.PageNumber, query.PageSize, total);
    }

    // The current prescribing screen has one prescription aggregate per examination.
    // Repeated POST returns that aggregate even after it has been sent.
    public async Task<PrescriptionResponse> CreatePrescriptionAsync(ClaimsPrincipal actor, int registrationId, CancellationToken ct)
    {
        var id = await WriteAsync(actor, registrationId, true, async registration =>
        {
            var record = await RecordAsync(registration, false, ct);
            var existing = await db.HerbalPrescriptions.Where(p => p.RecordId == record.RecordId)
                .OrderBy(p => p.PrescriptionId).Select(p => (int?)p.PrescriptionId).FirstOrDefaultAsync(ct);
            if (existing.HasValue) return existing.Value;
            await RecordAsync(registration, true, ct);
            var prescription = new HerbalPrescription
            {
                RecordId = record.RecordId, TotalDoses = 1, Status = "DRAFT", CreatedBy = UserId(actor), CreatedAt = DateTime.UtcNow
            };
            db.HerbalPrescriptions.Add(prescription);
            await db.SaveChangesAsync(ct);
            return prescription.PrescriptionId;
        }, ct);
        return await PrescriptionResponseAsync(id, ct);
    }

    private async Task<HerbalPrescription> PrescriptionAsync(int recordId, int id, CancellationToken ct) =>
        await db.HerbalPrescriptions.Include(p => p.HerbalPrescriptionDetails)
            .SingleOrDefaultAsync(p => p.RecordId == recordId && p.PrescriptionId == id, ct)
        ?? throw new NotFoundException("Prescription does not belong to this examination.");

    private static void Draft(string status)
    {
        if (status != "DRAFT") throw new ConflictException("Only DRAFT content can be changed or deleted.");
    }

    public async Task<PrescriptionResponse> SavePrescriptionAsync(ClaimsPrincipal actor, int registrationId, int prescriptionId,
        SavePrescriptionRequest request, CancellationToken ct)
    {
        Validate(request);
        foreach (var item in request.Items) Validate(item);
        if (request.Items.Select(i => i.HerbId).Distinct().Count() != request.Items.Count)
            throw new BadRequestException("Each herb may appear only once in a prescription.");
        if (request.Items.Any(i => decimal.Round(i.DosagePerDose, 3) != i.DosagePerDose))
            throw new BadRequestException("DosagePerDose supports at most three decimal places.");
        await WriteAsync(actor, registrationId, true, async registration =>
        {
            var record = await RecordAsync(registration, true, ct);
            var prescription = await PrescriptionAsync(record.RecordId, prescriptionId, ct);
            Draft(prescription.Status);
            Version(prescription.RowVersion, request.RowVersion);
            var ids = request.Items.Select(i => i.HerbId).ToArray();
            var herbs = await db.Herbs.Where(h => ids.Contains(h.HerbId) && h.IsActive)
                .Select(h => new { h.HerbId, h.CommonName }).ToDictionaryAsync(h => h.HerbId, ct);
            if (herbs.Count != ids.Length) throw new BadRequestException("All herbs must exist and be active.");
            prescription.PrescriptionName = request.PrescriptionName;
            prescription.TotalDoses = request.TotalDoses;
            prescription.DosageDays = request.DosageDays;
            prescription.UsageInstructions = request.UsageInstructions;
            prescription.Note = request.Note;
            prescription.UpdatedAt = DateTime.UtcNow;
            // Synchronize by HerbId so unchanged detail identities and snapshots are preserved.
            foreach (var detail in prescription.HerbalPrescriptionDetails.ToList())
                if (!ids.Contains(detail.HerbId)) db.HerbalPrescriptionDetails.Remove(detail);
            foreach (var item in request.Items)
            {
                var detail = prescription.HerbalPrescriptionDetails.SingleOrDefault(d => d.HerbId == item.HerbId);
                if (detail == null)
                {
                    detail = new HerbalPrescriptionDetail { HerbId = item.HerbId, HerbNameSnapshot = herbs[item.HerbId].CommonName };
                    prescription.HerbalPrescriptionDetails.Add(detail);
                }
                detail.DosagePerDose = item.DosagePerDose;
                detail.RoleInFormula = item.RoleInFormula;
                detail.Note = item.Note;
            }
            return true;
        }, ct);
        return await PrescriptionResponseAsync(prescriptionId, ct);
    }

    public async Task<PrescriptionResponse> LoadFormulaAsync(ClaimsPrincipal actor, int registrationId, int prescriptionId,
        LoadFormulaRequest request, CancellationToken ct)
    {
        Validate(request);
        await WriteAsync(actor, registrationId, true, async registration =>
        {
            var record = await RecordAsync(registration, true, ct);
            var prescription = await PrescriptionAsync(record.RecordId, prescriptionId, ct);
            Draft(prescription.Status);
            Version(prescription.RowVersion, request.RowVersion);
            var formula = await db.HerbalFormulas.AsNoTracking().Include(f => f.HerbalFormulaDetails).ThenInclude(d => d.Herb)
                .SingleOrDefaultAsync(f => f.HerbalFormulaId == request.HerbalFormulaId && f.Status == "APPROVED" && f.IsActive, ct)
                ?? throw new BadRequestException("Only active APPROVED formulas may be loaded.");
            if (formula.HerbalFormulaDetails.Count == 0 || formula.HerbalFormulaDetails.Any(d => !d.Herb.IsActive || d.DosagePerDose <= 0))
                throw new BadRequestException("The formula must contain active herbs with positive dosages.");
            var ids = formula.HerbalFormulaDetails.Select(d => d.HerbId).ToHashSet();
            foreach (var detail in prescription.HerbalPrescriptionDetails.ToList())
                if (!ids.Contains(detail.HerbId)) db.HerbalPrescriptionDetails.Remove(detail);
            foreach (var source in formula.HerbalFormulaDetails)
            {
                var detail = prescription.HerbalPrescriptionDetails.SingleOrDefault(d => d.HerbId == source.HerbId);
                if (detail == null)
                {
                    detail = new HerbalPrescriptionDetail { HerbId = source.HerbId, HerbNameSnapshot = source.Herb.CommonName };
                    prescription.HerbalPrescriptionDetails.Add(detail);
                }
                detail.DosagePerDose = source.DosagePerDose;
                detail.RoleInFormula = source.RoleInFormula;
                detail.Note = source.Note;
            }
            prescription.SourceHerbalFormulaId = formula.HerbalFormulaId;
            prescription.PrescriptionName = formula.FormulaName;
            prescription.UsageInstructions = formula.UsageInstructions;
            prescription.UpdatedAt = DateTime.UtcNow;
            return true;
        }, ct);
        return await PrescriptionResponseAsync(prescriptionId, ct);
    }

    public async Task<PrescriptionResponse> SendPrescriptionAsync(ClaimsPrincipal actor, int registrationId, int prescriptionId,
        VersionRequest request, CancellationToken ct)
    {
        Validate(request);
        await WriteAsync(actor, registrationId, true, async registration =>
        {
            var record = await RecordAsync(registration, false, ct);
            var prescription = await PrescriptionAsync(record.RecordId, prescriptionId, ct);
            if (prescription.SentAt.HasValue && prescription.Status != "DRAFT") return true;
            await RecordAsync(registration, true, ct);
            Draft(prescription.Status);
            Version(prescription.RowVersion, request.RowVersion);
            if (prescription.TotalDoses <= 0 || string.IsNullOrWhiteSpace(prescription.UsageInstructions)
                || prescription.HerbalPrescriptionDetails.Count == 0 || prescription.HerbalPrescriptionDetails.Any(d => d.DosagePerDose <= 0))
                throw new BadRequestException("A prescription requires positive TotalDoses, usage instructions and at least one positive herb dosage.");
            var ids = prescription.HerbalPrescriptionDetails.Select(d => d.HerbId).ToArray();
            if (await db.Herbs.CountAsync(h => ids.Contains(h.HerbId) && h.IsActive, ct) != ids.Length)
                throw new BadRequestException("The prescription contains inactive herbs.");
            prescription.Status = "SENT";
            prescription.SentAt = DateTime.UtcNow;
            prescription.UpdatedAt = prescription.SentAt;
            return true;
        }, ct);
        return await PrescriptionResponseAsync(prescriptionId, ct);
    }

    public async Task DeletePrescriptionAsync(ClaimsPrincipal actor, int registrationId, int prescriptionId, VersionRequest request, CancellationToken ct)
    {
        Validate(request);
        await WriteAsync(actor, registrationId, true, async registration =>
        {
            var record = await RecordAsync(registration, true, ct);
            var prescription = await PrescriptionAsync(record.RecordId, prescriptionId, ct);
            Draft(prescription.Status);
            Version(prescription.RowVersion, request.RowVersion);
            db.HerbalPrescriptionDetails.RemoveRange(prescription.HerbalPrescriptionDetails);
            db.HerbalPrescriptions.Remove(prescription);
            return true;
        }, ct);
    }

    private IQueryable<PrescriptionResponse> PrescriptionsQuery(int recordId) => db.HerbalPrescriptions.AsNoTracking()
        .Where(p => p.RecordId == recordId).OrderBy(p => p.PrescriptionId)
        .Select(p => new PrescriptionResponse(p.PrescriptionId, p.PrescriptionCode, p.RecordId, p.SourceHerbalFormulaId,
            p.PrescriptionName, p.TotalDoses, p.DosageDays, p.UsageInstructions, p.Note, p.Status, p.SentAt, p.RowVersion,
            p.HerbalPrescriptionDetails.OrderBy(d => d.PrescriptionDetailId)
                .Select(d => new PrescriptionItemResponse(d.PrescriptionDetailId, d.HerbId, d.HerbNameSnapshot,
                    d.DosagePerDose, d.DosagePerDose * p.TotalDoses, d.RoleInFormula, d.Note)).ToList()));

    private Task<PrescriptionResponse> PrescriptionResponseAsync(int id, CancellationToken ct) => db.HerbalPrescriptions.AsNoTracking()
        .Where(p => p.PrescriptionId == id)
        .Select(p => new PrescriptionResponse(p.PrescriptionId, p.PrescriptionCode, p.RecordId, p.SourceHerbalFormulaId,
            p.PrescriptionName, p.TotalDoses, p.DosageDays, p.UsageInstructions, p.Note, p.Status, p.SentAt, p.RowVersion,
            p.HerbalPrescriptionDetails.OrderBy(d => d.PrescriptionDetailId)
                .Select(d => new PrescriptionItemResponse(d.PrescriptionDetailId, d.HerbId, d.HerbNameSnapshot,
                    d.DosagePerDose, d.DosagePerDose * p.TotalDoses, d.RoleInFormula, d.Note)).ToList())).SingleAsync(ct);
}

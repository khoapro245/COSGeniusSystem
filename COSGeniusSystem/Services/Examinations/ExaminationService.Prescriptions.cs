using System.Security.Claims;
using COSGeniusSystem.Commons;
using COSGeniusSystem.DTOs;
using COSGeniusSystem.Exceptions;
using COSGeniusSystem.Models;
using Microsoft.EntityFrameworkCore;

namespace COSGeniusSystem.Services;

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
        var formulas = await q.OrderBy(f => f.FormulaName).ThenBy(f => f.HerbalFormulaId)
            .Skip((query.PageNumber - 1) * query.PageSize).Take(query.PageSize)
            .Include(f => f.HerbalFormulaDetails)
                .ThenInclude(d => d.Herb)
            .ToListAsync(ct);

        var herbIds = formulas.SelectMany(f => f.HerbalFormulaDetails.Select(d => d.HerbId)).Distinct().ToList();
        var stockMap = await COSGeniusSystem.Services.Herbs.HerbStockHelper.GetStockMapAsync(db, herbIds, null, ct);

        var items = formulas.Select(f => new FormulaResponse(
            f.HerbalFormulaId, f.FormulaCode, f.FormulaName, f.IntendedUse, f.UsageInstructions,
            f.HerbalFormulaDetails.OrderBy(d => d.HerbalFormulaDetailId)
                .Select(d => new FormulaItemResponse(
                    d.HerbId,
                    d.Herb.CommonName,
                    d.DosagePerDose,
                    d.RoleInFormula,
                    d.Note,
                    d.Herb.IsActive,
                    d.Herb.StandardPrice,
                    stockMap.GetValueOrDefault(d.HerbId)?.AvailableStock ?? 0m
                )).ToList()
        )).ToList();

        return new(items, query.PageNumber, query.PageSize, total);
    }

    public async Task<PrescriptionResponse> CreatePrescriptionAsync(ClaimsPrincipal actor, int registrationId, CancellationToken ct)
    {
        var id = await WriteAsync(actor, registrationId, true, async registration =>
        {
            var record = await RecordAsync(registration, true, ct);
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
        ?? throw new NotFoundException("The prescription does not belong to this examination.");

    private static void Draft(string status)
    {
        if (status != "DRAFT") throw new ConflictException("Only draft content can be edited or deleted.");
    }

    public async Task<PrescriptionResponse> SavePrescriptionAsync(ClaimsPrincipal actor, int registrationId, int prescriptionId,
        SavePrescriptionRequest request, CancellationToken ct)
    {
        Validate(request);
        foreach (var item in request.Items) Validate(item);
        if (request.Items.Select(i => i.HerbId).Distinct().Count() != request.Items.Count)
            throw new BadRequestException("Each herb may appear only once in a prescription.");
        if (request.Items.Any(i => decimal.Round(i.DosagePerDose, 3) != i.DosagePerDose))
            throw new BadRequestException("The dose per pack may have at most 3 decimal places.");
        await WriteAsync(actor, registrationId, true, async registration =>
        {
            var record = await RecordAsync(registration, true, ct);
            var prescription = await PrescriptionAsync(record.RecordId, prescriptionId, ct);
            if (prescription.Status is not ("DRAFT" or "SENT"))
                throw new ConflictException("Examination content cannot be edited in the current status.");
            Version(prescription.RowVersion, request.RowVersion);
            if (request.Items.Count == 0)
                throw new BadRequestException("Select and apply a formula or add at least one herb before saving a draft prescription.");
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
            if (prescription.Status is not ("DRAFT" or "SENT"))
                throw new ConflictException("Examination content cannot be edited in the current status.");
            Version(prescription.RowVersion, request.RowVersion);
            var formula = await db.HerbalFormulas.AsNoTracking().Include(f => f.HerbalFormulaDetails).ThenInclude(d => d.Herb)
                .SingleOrDefaultAsync(f => f.HerbalFormulaId == request.HerbalFormulaId && f.Status == "APPROVED" && f.IsActive, ct)
                ?? throw new BadRequestException("Only active and approved formulas can be used.");
            if (formula.HerbalFormulaDetails.Count == 0 || formula.HerbalFormulaDetails.Any(d => !d.Herb.IsActive || d.DosagePerDose <= 0))
                throw new BadRequestException("A formula must contain active herbs with positive doses.");
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
            await SendPrescriptionContentAsync(prescription, ct);
            return true;
        }, ct);
        return await PrescriptionResponseAsync(prescriptionId, ct);
    }

    private async Task SendPrescriptionContentAsync(HerbalPrescription prescription, CancellationToken ct, bool checkStock = true)
    {
        if (prescription.TotalDoses <= 0 || string.IsNullOrWhiteSpace(prescription.UsageInstructions)
            || prescription.HerbalPrescriptionDetails.Count == 0 || prescription.HerbalPrescriptionDetails.Any(d => d.DosagePerDose <= 0))
            throw new BadRequestException("A prescription requires a positive pack count, usage instructions, and at least one herb with a positive dose.");
        var ids = prescription.HerbalPrescriptionDetails.Select(d => d.HerbId).Distinct().ToArray();
        if (await db.Herbs.CountAsync(h => ids.Contains(h.HerbId) && h.IsActive, ct) != ids.Length)
            throw new BadRequestException("The prescription contains an inactive herb.");

        if (checkStock)
        {
            // Kiểm soát giữ chỗ nguyên tử & Tồn khả dụng theo thời gian thực
            var stockMap = await COSGeniusSystem.Services.Herbs.HerbStockHelper.GetStockMapAsync(db, ids, prescription.PrescriptionId, ct);
            var shortages = new List<string>();
            foreach (var detail in prescription.HerbalPrescriptionDetails)
            {
                var requiredQuantity = detail.DosagePerDose * prescription.TotalDoses;
                var availableQuantity = stockMap.GetValueOrDefault(detail.HerbId)?.AvailableStock ?? 0m;
                if (requiredQuantity > availableQuantity)
                {
                    var name = !string.IsNullOrWhiteSpace(detail.HerbNameSnapshot)
                        ? detail.HerbNameSnapshot
                        : (await db.Herbs.Where(h => h.HerbId == detail.HerbId).Select(h => h.CommonName).FirstOrDefaultAsync(ct) ?? $"ID {detail.HerbId}");
                    shortages.Add($"'{name}' (cần: {requiredQuantity:0.###}g, khả dụng: {availableQuantity:0.###}g)");
                }
            }
            if (shortages.Count > 0)
            {
                throw new ConflictException($"Không đủ tồn khả dụng cho các vị thuốc: {string.Join(", ", shortages)}. Vui lòng kiểm tra và điều chỉnh đơn thuốc.");
            }

        }

        prescription.Status = "SENT";
        prescription.SentAt ??= DateTime.UtcNow;
        prescription.UpdatedAt = prescription.SentAt;
    }

    public async Task DeletePrescriptionAsync(ClaimsPrincipal actor, int registrationId, int prescriptionId, VersionRequest? request, CancellationToken ct)
    {
        await WriteAsync(actor, registrationId, true, async registration =>
        {
            var record = await RecordAsync(registration, true, ct);
            var prescription = await PrescriptionAsync(record.RecordId, prescriptionId, ct);
            Draft(prescription.Status);
            if (request?.RowVersion != null && request.RowVersion.Length == 8)
                Version(prescription.RowVersion, request.RowVersion);
            db.HerbalPrescriptionDetails.RemoveRange(prescription.HerbalPrescriptionDetails);
            db.HerbalPrescriptions.Remove(prescription);
            return true;
        }, ct);
    }

    private async Task<IReadOnlyList<PrescriptionResponse>> PrescriptionsForRecordAsync(int recordId, CancellationToken ct)
    {
        var prescriptions = await db.HerbalPrescriptions.AsNoTracking()
            .Where(p => p.RecordId == recordId)
            .OrderBy(p => p.PrescriptionId)
            .Include(p => p.HerbalPrescriptionDetails)
                .ThenInclude(d => d.Herb)
            .ToListAsync(ct);

        if (prescriptions.Count == 0) return [];

        var allHerbIds = prescriptions.SelectMany(p => p.HerbalPrescriptionDetails.Select(d => d.HerbId)).Distinct().ToList();
        var firstPrescriptionId = prescriptions.Count == 1 ? (int?)prescriptions[0].PrescriptionId : null;
        var stockMap = await COSGeniusSystem.Services.Herbs.HerbStockHelper.GetStockMapAsync(db, allHerbIds, firstPrescriptionId, ct);

        return prescriptions.Select(p => new PrescriptionResponse(
            p.PrescriptionId, p.PrescriptionCode, p.RecordId, p.SourceHerbalFormulaId,
            p.PrescriptionName, p.TotalDoses, p.DosageDays, p.UsageInstructions, p.Note, p.Status, p.SentAt, p.RowVersion,
            p.HerbalPrescriptionDetails.OrderBy(d => d.PrescriptionDetailId)
                .Select(d => new PrescriptionItemResponse(
                    d.PrescriptionDetailId,
                    d.HerbId,
                    d.HerbNameSnapshot,
                    d.DosagePerDose,
                    d.DosagePerDose * p.TotalDoses,
                    d.RoleInFormula,
                    d.Note,
                    d.Herb?.StandardPrice ?? 0m,
                    stockMap.GetValueOrDefault(d.HerbId)?.AvailableStock ?? 0m
                )).ToList()
        )).ToList();
    }

    private async Task<PrescriptionResponse> PrescriptionResponseAsync(int id, CancellationToken ct)
    {
        var p = await db.HerbalPrescriptions.AsNoTracking()
            .Where(x => x.PrescriptionId == id)
            .Include(x => x.HerbalPrescriptionDetails)
                .ThenInclude(d => d.Herb)
            .SingleAsync(ct);

        var herbIds = p.HerbalPrescriptionDetails.Select(d => d.HerbId).Distinct().ToList();
        var stockMap = await COSGeniusSystem.Services.Herbs.HerbStockHelper.GetStockMapAsync(db, herbIds, p.PrescriptionId, ct);

        return new PrescriptionResponse(
            p.PrescriptionId, p.PrescriptionCode, p.RecordId, p.SourceHerbalFormulaId,
            p.PrescriptionName, p.TotalDoses, p.DosageDays, p.UsageInstructions, p.Note, p.Status, p.SentAt, p.RowVersion,
            p.HerbalPrescriptionDetails.OrderBy(d => d.PrescriptionDetailId)
                .Select(d => new PrescriptionItemResponse(
                    d.PrescriptionDetailId,
                    d.HerbId,
                    d.HerbNameSnapshot,
                    d.DosagePerDose,
                    d.DosagePerDose * p.TotalDoses,
                    d.RoleInFormula,
                    d.Note,
                    d.Herb?.StandardPrice ?? 0m,
                    stockMap.GetValueOrDefault(d.HerbId)?.AvailableStock ?? 0m
                )).ToList()
        );
    }
}

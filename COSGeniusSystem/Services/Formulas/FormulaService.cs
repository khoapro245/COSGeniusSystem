using System.Security.Claims;
using COSGeniusSystem.Commons;
using COSGeniusSystem.Data;
using COSGeniusSystem.DTOs.Formulas;
using COSGeniusSystem.Exceptions;
using COSGeniusSystem.Models;
using Microsoft.EntityFrameworkCore;

namespace COSGeniusSystem.Services.Formulas;

public class FormulaService : IFormulaService
{
    private readonly ApplicationDbContext _db;

    public FormulaService(ApplicationDbContext db)
    {
        _db = db;
    }

    private static int GetUserId(ClaimsPrincipal actor)
    {
        var claim = actor.FindFirst(ClaimTypes.NameIdentifier) ?? actor.FindFirst("sub");
        return int.TryParse(claim?.Value, out var id) && id > 0
            ? id
            : throw new UnauthorizedException("User identity not found in token.");
    }

    public async Task<List<HerbalFormulaDto>> GetFormulasAsync(FormulaFilterQuery query, CancellationToken ct = default)
    {
        var q = _db.HerbalFormulas
            .Include(f => f.HerbalFormulaDetails)
                .ThenInclude(d => d.Herb)
            .Include(f => f.ProposedByDoctor)
            .Include(f => f.ReviewedByNavigation)
            .AsNoTracking();

        if (!string.IsNullOrWhiteSpace(query.Status) && !query.Status.Equals("ALL", StringComparison.OrdinalIgnoreCase))
        {
            var statusUpper = query.Status.Trim().ToUpperInvariant();
            q = q.Where(f => f.Status == statusUpper);
        }

        if (query.IsActive.HasValue)
        {
            q = q.Where(f => f.IsActive == query.IsActive.Value);
        }

        if (!string.IsNullOrWhiteSpace(query.Keyword))
        {
            var kw = query.Keyword.Trim();
            q = q.Where(f => f.FormulaName.Contains(kw) || (f.FormulaCode != null && f.FormulaCode.Contains(kw)));
        }

        var formulas = await q
            .OrderByDescending(f => f.CreatedAt)
            .Skip((query.Page - 1) * query.PageSize)
            .Take(query.PageSize)
            .ToListAsync(ct);

        // Preload base formulas if needed for calculating previous dosages
        var allApprovedFormulas = await _db.HerbalFormulas
            .Include(f => f.HerbalFormulaDetails)
            .Where(f => f.Status == "APPROVED")
            .AsNoTracking()
            .ToListAsync(ct);

        return formulas.Select(f => MapToDto(f, allApprovedFormulas)).ToList();
    }

    public async Task<HerbalFormulaDto> GetFormulaByIdAsync(int id, CancellationToken ct = default)
    {
        var formula = await _db.HerbalFormulas
            .Include(f => f.HerbalFormulaDetails)
                .ThenInclude(d => d.Herb)
            .Include(f => f.ProposedByDoctor)
            .Include(f => f.ReviewedByNavigation)
            .AsNoTracking()
            .FirstOrDefaultAsync(f => f.HerbalFormulaId == id, ct);

        if (formula == null)
            throw new NotFoundException($"Herbal formula with ID {id} not found.");

        var allApproved = await _db.HerbalFormulas
            .Include(f => f.HerbalFormulaDetails)
            .Where(f => f.Status == "APPROVED")
            .AsNoTracking()
            .ToListAsync(ct);

        return MapToDto(formula, allApproved);
    }

    public async Task<HerbalFormulaDto> CreateFormulaAsync(ClaimsPrincipal actor, CreateHerbalFormulaRequest request, CancellationToken ct = default)
    {
        var userId = GetUserId(actor);
        var isManager = actor.IsInRole(RoleConstants.Manager) || actor.IsInRole(RoleConstants.Admin);

        var doctor = await _db.Doctors.FirstOrDefaultAsync(d => d.UserId == userId, ct)
                     ?? await _db.Doctors.FirstOrDefaultAsync(d => d.IsActive, ct)
                     ?? await _db.Doctors.FirstOrDefaultAsync(ct);

        if (doctor == null)
            throw new BadRequestException("No doctor account available to associate with the formula.");

        // Generate FormulaCode if not provided
        string formulaCode;
        if (!string.IsNullOrWhiteSpace(request.FormulaCode))
        {
            var existing = await _db.HerbalFormulas.AnyAsync(f => f.FormulaCode == request.FormulaCode.Trim(), ct);
            if (existing)
                throw new ConflictException($"Formula code '{request.FormulaCode}' already exists.");
            formulaCode = request.FormulaCode.Trim();
        }
        else
        {
            var prefix = isManager ? "CT" : "DX";
            var maxId = await _db.HerbalFormulas.MaxAsync(f => (int?)f.HerbalFormulaId, ct) ?? 0;
            formulaCode = $"{prefix}{(maxId + 1):D5}";
        }

        var status = isManager ? "APPROVED" : "PENDING";
        var version = !string.IsNullOrWhiteSpace(request.Version) ? request.Version : (isManager ? "v1" : "v1");

        // Encode version and proposal type in review note if provided
        var reviewNote = "";
        if (!string.IsNullOrWhiteSpace(request.ProposalType) && request.ProposalType.Equals("UPDATE", StringComparison.OrdinalIgnoreCase))
        {
            reviewNote = $"[UPDATE:{request.BaseFormulaCode ?? ""}:{version}]";
        }
        else
        {
            reviewNote = $"[VERSION:{version}]";
        }

        var formula = new HerbalFormula
        {
            FormulaCode = formulaCode,
            FormulaName = request.FormulaName.Trim(),
            UsageInstructions = request.UsageInstructions,
            IntendedUse = request.IntendedUse,
            Status = status,
            ProposedByDoctorId = doctor.DoctorId,
            SubmittedAt = isManager ? null : DateTime.UtcNow,
            ReviewedBy = isManager ? userId : null,
            ReviewedAt = isManager ? DateTime.UtcNow : null,
            ReviewNote = reviewNote,
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
        };

        if (request.Items != null && request.Items.Count > 0)
        {
            foreach (var item in request.Items)
            {
                var herb = await _db.Herbs.FindAsync([item.HerbId], ct);
                if (herb == null)
                    throw new BadRequestException($"Herb with ID {item.HerbId} does not exist.");

                formula.HerbalFormulaDetails.Add(new HerbalFormulaDetail
                {
                    HerbId = item.HerbId,
                    DosagePerDose = item.DosagePerDose,
                    RoleInFormula = item.RoleInFormula,
                    Note = item.Note,
                });
            }
        }

        _db.HerbalFormulas.Add(formula);
        await _db.SaveChangesAsync(ct);

        return await GetFormulaByIdAsync(formula.HerbalFormulaId, ct);
    }

    public async Task<HerbalFormulaDto> UpdateFormulaAsync(ClaimsPrincipal actor, int id, UpdateHerbalFormulaRequest request, CancellationToken ct = default)
    {
        var formula = await _db.HerbalFormulas
            .Include(f => f.HerbalFormulaDetails)
            .FirstOrDefaultAsync(f => f.HerbalFormulaId == id, ct);

        if (formula == null)
            throw new NotFoundException($"Herbal formula with ID {id} not found.");

        formula.FormulaName = request.FormulaName.Trim();
        formula.UsageInstructions = request.UsageInstructions;
        formula.IntendedUse = request.IntendedUse;
        formula.UpdatedAt = DateTime.UtcNow;

        if (request.IsActive.HasValue)
        {
            formula.IsActive = request.IsActive.Value;
        }

        if (!string.IsNullOrWhiteSpace(request.Version))
        {
            formula.ReviewNote = $"[VERSION:{request.Version}] {StripVersionPrefix(formula.ReviewNote)}";
        }

        // Replace ingredients
        if (request.Items != null)
        {
            _db.HerbalFormulaDetails.RemoveRange(formula.HerbalFormulaDetails);
            foreach (var item in request.Items)
            {
                var herb = await _db.Herbs.FindAsync([item.HerbId], ct);
                if (herb == null)
                    throw new BadRequestException($"Herb with ID {item.HerbId} does not exist.");

                formula.HerbalFormulaDetails.Add(new HerbalFormulaDetail
                {
                    HerbalFormulaId = formula.HerbalFormulaId,
                    HerbId = item.HerbId,
                    DosagePerDose = item.DosagePerDose,
                    RoleInFormula = item.RoleInFormula,
                    Note = item.Note,
                });
            }
        }

        await _db.SaveChangesAsync(ct);
        return await GetFormulaByIdAsync(formula.HerbalFormulaId, ct);
    }

    public async Task<HerbalFormulaDto> ToggleFormulaStatusAsync(ClaimsPrincipal actor, int id, CancellationToken ct = default)
    {
        var formula = await _db.HerbalFormulas
            .Include(f => f.HerbalFormulaDetails)
            .FirstOrDefaultAsync(f => f.HerbalFormulaId == id, ct);

        if (formula == null)
            throw new NotFoundException($"Herbal formula with ID {id} not found.");

        // Rule: Không được xóa đơn thuốc, chỉ vô hiệu hóa
        formula.IsActive = !formula.IsActive;
        formula.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync(ct);
        return await GetFormulaByIdAsync(formula.HerbalFormulaId, ct);
    }

    public async Task<HerbalFormulaDto> ReviewFormulaAsync(ClaimsPrincipal actor, int id, ReviewFormulaRequest request, CancellationToken ct = default)
    {
        var formula = await _db.HerbalFormulas
            .Include(f => f.HerbalFormulaDetails)
            .FirstOrDefaultAsync(f => f.HerbalFormulaId == id, ct);

        if (formula == null)
            throw new NotFoundException($"Herbal formula proposal with ID {id} not found.");

        if (formula.Status != "PENDING")
            throw new BadRequestException($"Formula proposal status is '{formula.Status}' and cannot be reviewed again.");

        var userId = GetUserId(actor);
        formula.ReviewedBy = userId;
        formula.ReviewedAt = DateTime.UtcNow;
        formula.ReviewNote = (formula.ReviewNote ?? "") + (string.IsNullOrWhiteSpace(request.ReviewNote) ? "" : $" | {request.ReviewNote.Trim()}");
        formula.UpdatedAt = DateTime.UtcNow;

        if (request.Approved)
        {
            formula.Status = "APPROVED";
            formula.IsActive = true;

            // If this was an update to an existing formula, check if base formula exists
            var baseCode = ExtractBaseFormulaCode(formula.ReviewNote, formula.FormulaCode);
            if (!string.IsNullOrWhiteSpace(baseCode))
            {
                var baseFormula = await _db.HerbalFormulas
                    .Include(f => f.HerbalFormulaDetails)
                    .FirstOrDefaultAsync(f => f.FormulaCode == baseCode, ct);

                if (baseFormula != null)
                {
                    // Update base formula with new ingredients and version
                    baseFormula.FormulaName = formula.FormulaName;
                    baseFormula.UsageInstructions = formula.UsageInstructions;
                    baseFormula.IntendedUse = formula.IntendedUse;
                    baseFormula.UpdatedAt = DateTime.UtcNow;
                    var newVersion = ExtractVersion(formula.ReviewNote) ?? "v3";
                    baseFormula.ReviewNote = $"[VERSION:{newVersion}] Cập nhật từ đề xuất {formula.FormulaCode}";

                    _db.HerbalFormulaDetails.RemoveRange(baseFormula.HerbalFormulaDetails);
                    foreach (var d in formula.HerbalFormulaDetails)
                    {
                        baseFormula.HerbalFormulaDetails.Add(new HerbalFormulaDetail
                        {
                            HerbalFormulaId = baseFormula.HerbalFormulaId,
                            HerbId = d.HerbId,
                            DosagePerDose = d.DosagePerDose,
                            RoleInFormula = d.RoleInFormula,
                            Note = d.Note,
                        });
                    }
                }
            }
        }
        else
        {
            formula.Status = "REJECTED";
        }

        await _db.SaveChangesAsync(ct);
        return await GetFormulaByIdAsync(formula.HerbalFormulaId, ct);
    }

    private static HerbalFormulaDto MapToDto(HerbalFormula f, List<HerbalFormula>? approvedList = null)
    {
        var rawNote = f.ReviewNote ?? "";
        var version = ExtractVersion(rawNote);
        var baseCode = ExtractBaseFormulaCode(rawNote, f.FormulaCode);
        var isUpdate = rawNote.Contains("[UPDATE:") || (!string.IsNullOrWhiteSpace(baseCode) && f.Status == "PENDING");
        var proposalType = isUpdate ? $"Sửa {ExtractFromVersion(rawNote) ?? "v2"} → {version ?? "v3"}" : "Tạo mới";

        var displayCode = f.Status == "PENDING"
            ? $"DX{f.HerbalFormulaId:D5}"
            : (f.FormulaCode != null && f.FormulaCode.StartsWith("CT")
                ? $"CT{f.HerbalFormulaId:D5}"
                : (f.FormulaCode ?? $"CT{f.HerbalFormulaId:D5}"));

        HerbalFormula? baseFormula = null;
        if (!string.IsNullOrWhiteSpace(baseCode) && approvedList != null)
        {
            baseFormula = approvedList.FirstOrDefault(a => 
                a.FormulaCode == baseCode || 
                $"CT{a.HerbalFormulaId:D5}" == baseCode ||
                $"CT{a.HerbalFormulaId}" == baseCode ||
                a.FormulaName == f.FormulaName);
        }

        var items = f.HerbalFormulaDetails.Select(d =>
        {
            decimal? prevDosage = null;
            if (baseFormula != null)
            {
                var matched = baseFormula.HerbalFormulaDetails.FirstOrDefault(b => b.HerbId == d.HerbId);
                if (matched != null)
                {
                    prevDosage = matched.DosagePerDose;
                }
            }
            return new HerbalFormulaItemDto(
                d.HerbalFormulaDetailId,
                d.HerbId,
                d.Herb?.HerbCode ?? $"VT{d.HerbId:D5}",
                d.Herb?.CommonName ?? "Vị thuốc",
                d.Herb?.Unit ?? "g",
                d.DosagePerDose,
                prevDosage,
                d.RoleInFormula,
                d.Note,
                d.Herb?.IsActive ?? true
            );
        }).ToList();

        var displayBaseCode = baseFormula != null ? $"CT{baseFormula.HerbalFormulaId:D5}" : baseCode;

        return new HerbalFormulaDto(
            f.HerbalFormulaId,
            displayCode,
            f.FormulaName,
            version ?? (f.Status == "APPROVED" ? "v1" : "v1"),
            f.Status == "PENDING" ? proposalType : null,
            displayBaseCode,
            f.UsageInstructions,
            f.IntendedUse,
            f.Status,
            f.ProposedByDoctorId,
            f.ProposedByDoctor?.FullName ?? "BS. Lê Minh",
            f.SubmittedAt ?? f.CreatedAt,
            f.ReviewedBy,
            f.ReviewedByNavigation?.FullName ?? "Quản lý",
            f.ReviewedAt,
            StripVersionPrefix(f.ReviewNote),
            f.IsActive,
            f.CreatedAt,
            f.UpdatedAt,
            f.RowVersion,
            items
        );
    }

    private static string? ExtractVersion(string note)
    {
        if (string.IsNullOrWhiteSpace(note)) return null;
        if (note.Contains("[VERSION:"))
        {
            var start = note.IndexOf("[VERSION:", StringComparison.Ordinal) + 9;
            var end = note.IndexOf(']', start);
            if (end > start) return note.Substring(start, end - start).Trim();
        }
        if (note.Contains("->") || note.Contains("→"))
        {
            var idx = note.LastIndexOfAny(new[] { '>', '→' });
            if (idx >= 0 && idx + 1 < note.Length)
            {
                var part = note.Substring(idx + 1).Trim().Split(new[] { ']', ' ' })[0];
                if (!string.IsNullOrWhiteSpace(part)) return part;
            }
        }
        return null;
    }

    private static string? ExtractFromVersion(string note)
    {
        if (string.IsNullOrWhiteSpace(note)) return null;
        if (note.Contains("->") || note.Contains("→"))
        {
            var arrowIdx = note.IndexOfAny(new[] { '-', '→' });
            if (arrowIdx > 0)
            {
                var before = note.Substring(0, arrowIdx);
                var colonIdx = before.LastIndexOf(':');
                if (colonIdx >= 0) return before.Substring(colonIdx + 1).Trim();
            }
        }
        return "v2";
    }

    private static string? ExtractBaseFormulaCode(string note, string? currentCode)
    {
        if (string.IsNullOrWhiteSpace(note)) return null;
        if (note.Contains("[UPDATE:"))
        {
            var start = note.IndexOf("[UPDATE:", StringComparison.Ordinal) + 8;
            var end = note.IndexOf(':', start);
            if (end > start) return note.Substring(start, end - start).Trim();
        }
        return null;
    }

    private static string StripVersionPrefix(string? note)
    {
        if (string.IsNullOrWhiteSpace(note)) return "";
        if (note.StartsWith("["))
        {
            var closeIdx = note.IndexOf(']');
            if (closeIdx >= 0 && closeIdx + 1 < note.Length)
            {
                return note.Substring(closeIdx + 1).Trim();
            }
            if (closeIdx == note.Length - 1) return "";
        }
        return note.Trim();
    }
}

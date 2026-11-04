using System.Security.Claims;
using COSGeniusSystem.DTOs.Formulas;

namespace COSGeniusSystem.Services.Formulas;

public interface IFormulaService
{
    Task<List<HerbalFormulaDto>> GetFormulasAsync(FormulaFilterQuery query, CancellationToken ct = default);
    Task<HerbalFormulaDto> GetFormulaByIdAsync(int id, CancellationToken ct = default);
    Task<HerbalFormulaDto> CreateFormulaAsync(ClaimsPrincipal actor, CreateHerbalFormulaRequest request, CancellationToken ct = default);
    Task<HerbalFormulaDto> UpdateFormulaAsync(ClaimsPrincipal actor, int id, UpdateHerbalFormulaRequest request, CancellationToken ct = default);
    Task<HerbalFormulaDto> ToggleFormulaStatusAsync(ClaimsPrincipal actor, int id, CancellationToken ct = default);
    Task<HerbalFormulaDto> ReviewFormulaAsync(ClaimsPrincipal actor, int id, ReviewFormulaRequest request, CancellationToken ct = default);
}


using System.Security.Claims;
using COSGeniusSystem.Commons;
using COSGeniusSystem.DTOs.Formulas;
using COSGeniusSystem.Services.Formulas;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace COSGeniusSystem.Controllers;

[ApiController]
[Route("api/v1/formulas")]
public class FormulasController : BaseApiController
{
    private readonly IFormulaService _formulaService;

    public FormulasController(IFormulaService formulaService)
    {
        _formulaService = formulaService;
    }

    /// <summary>
    /// API-FORMULA-01: Lấy danh sách công thức thuốc & yêu cầu đề xuất
    /// GET /api/v1/formulas
    /// </summary>
    [HttpGet]
    [Authorize(Roles = $"{RoleConstants.Manager},{RoleConstants.Admin},{RoleConstants.Doctor}")]
    public async Task<ActionResult<ApiResponseModel<List<HerbalFormulaDto>>>> GetFormulas(
        [FromQuery] FormulaFilterQuery query, CancellationToken ct = default)
    {
        var result = await _formulaService.GetFormulasAsync(query, ct);
        return SuccessResponse(SystemMessages.FormulasRetrieved, result);
    }

    /// <summary>
    /// API-FORMULA-02: Lấy chi tiết công thức thuốc kèm thành phần
    /// GET /api/v1/formulas/{id}
    /// </summary>
    [HttpGet("{id:int}")]
    [Authorize(Roles = $"{RoleConstants.Manager},{RoleConstants.Admin},{RoleConstants.Doctor}")]
    public async Task<ActionResult<ApiResponseModel<HerbalFormulaDto>>> GetFormulaById(
        int id, CancellationToken ct = default)
    {
        var result = await _formulaService.GetFormulaByIdAsync(id, ct);
        return SuccessResponse(SystemMessages.FormulaRetrieved, result);
    }

    /// <summary>
    /// API-FORMULA-03: Tạo công thức thuốc mới (Manager) hoặc gửi đề xuất công thức mới (Doctor)
    /// POST /api/v1/formulas
    /// </summary>
    [HttpPost]
    [Authorize(Roles = $"{RoleConstants.Manager},{RoleConstants.Admin},{RoleConstants.Doctor}")]
    public async Task<ActionResult<ApiResponseModel<HerbalFormulaDto>>> CreateFormula(
        [FromBody] CreateHerbalFormulaRequest request, CancellationToken ct = default)
    {
        var result = await _formulaService.CreateFormulaAsync(User, request, ct);
        return SuccessResponse(SystemMessages.FormulaCreated, result);
    }

    /// <summary>
    /// API-FORMULA-04: Cập nhật thông tin công thức thuốc (Manager/Admin)
    /// PUT /api/v1/formulas/{id}
    /// </summary>
    [HttpPut("{id:int}")]
    [Authorize(Roles = $"{RoleConstants.Manager},{RoleConstants.Admin}")]
    public async Task<ActionResult<ApiResponseModel<HerbalFormulaDto>>> UpdateFormula(
        int id, [FromBody] UpdateHerbalFormulaRequest request, CancellationToken ct = default)
    {
        var result = await _formulaService.UpdateFormulaAsync(User, id, request, ct);
        return SuccessResponse(SystemMessages.FormulaUpdated, result);
    }

    /// <summary>
    /// API-FORMULA-05: Vô hiệu hóa hoặc kích hoạt lại công thức thuốc (Không xóa cứng)
    /// PATCH /api/v1/formulas/{id}/toggle-status
    /// </summary>
    [HttpPatch("{id:int}/toggle-status")]
    [Authorize(Roles = $"{RoleConstants.Manager},{RoleConstants.Admin}")]
    public async Task<ActionResult<ApiResponseModel<HerbalFormulaDto>>> ToggleFormulaStatus(
        int id, CancellationToken ct = default)
    {
        var result = await _formulaService.ToggleFormulaStatusAsync(User, id, ct);
        return SuccessResponse(SystemMessages.FormulaStatusUpdated, result);
    }

    /// <summary>
    /// API-FORMULA-06: Duyệt (APPROVED) hoặc Từ chối (REJECTED) đề xuất công thức thuốc từ bác sĩ
    /// POST /api/v1/formulas/{id}/review
    /// </summary>
    [HttpPost("{id:int}/review")]
    [Authorize(Roles = $"{RoleConstants.Manager},{RoleConstants.Admin}")]
    public async Task<ActionResult<ApiResponseModel<HerbalFormulaDto>>> ReviewFormula(
        int id, [FromBody] ReviewFormulaRequest request, CancellationToken ct = default)
    {
        var result = await _formulaService.ReviewFormulaAsync(User, id, request, ct);
        var msg = request.Approved ? SystemMessages.FormulaApproved : SystemMessages.FormulaRejected;
        return SuccessResponse(msg, result);
    }
}

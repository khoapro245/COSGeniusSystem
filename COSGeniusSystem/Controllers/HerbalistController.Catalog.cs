using COSGeniusSystem.DTOs;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace COSGeniusSystem.Controllers;

public partial class HerbalistController
{
    [HttpGet("patients")]
    public async Task<IActionResult> Patients([FromQuery] HerbalistFilter f, CancellationToken ct) =>
        RequisitionResponse(await requisitionService.PatientsAsync(f, ct));

    [HttpGet("patients/{id:int}")]
    public async Task<IActionResult> PatientDetail(int id, CancellationToken ct) =>
        RequisitionResponse(await requisitionService.PatientDetailAsync(id, ct));

    [HttpGet("herbs")]
    public async Task<IActionResult> Herbs([FromQuery] HerbalistFilter f, CancellationToken ct) =>
        RequisitionResponse(await requisitionService.HerbsAsync(f, ct));

    [HttpGet("herbs/{id:int}")]
    public async Task<IActionResult> HerbDetail(int id, CancellationToken ct) =>
        RequisitionResponse(await requisitionService.HerbDetailAsync(id, ct));

    [HttpGet("formulas")]
    public async Task<IActionResult> Formulas([FromQuery] HerbalistFilter f, CancellationToken ct) =>
        RequisitionResponse(await requisitionService.FormulasAsync(f, ct));

    [HttpGet("formulas/{id:int}")]
    public async Task<IActionResult> FormulaDetail(int id, CancellationToken ct) =>
        RequisitionResponse(await requisitionService.FormulaDetailAsync(id, ct));
}


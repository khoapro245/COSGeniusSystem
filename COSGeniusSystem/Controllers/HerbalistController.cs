using System.Security.Claims;
using COSGeniusSystem.Commons;
using COSGeniusSystem.Data;
using COSGeniusSystem.DTOs;
using COSGeniusSystem.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace COSGeniusSystem.Controllers;

[COSGeniusSystem.Services.Herbalist.HerbalistConflict]
[ApiController]
[Route("api/v1/herbalist")]
[Authorize(Roles = "ADMIN,PHARMACIST,HERBALIST")]
public partial class HerbalistController(COSGeniusSystem.Services.Herbalist.IHerbalistRequisitionService requisitionService) : ControllerBase
{
    private IActionResult Data(object? value) => Ok(new ApiResponseModel<object>(200, "Thành công.", true, value));
    private IActionResult Failure(int status, string message) => StatusCode(status, new ApiResponseModel<object>(status, message, false, null));
    private int Actor => int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier) ?? throw new InvalidOperationException("The user identifier is missing."));

    [HttpGet("capabilities")]
    public async Task<IActionResult> Capabilities(CancellationToken ct) =>
        RequisitionResponse(await requisitionService.CapabilitiesAsync(ct));

    [HttpGet("prescriptions")]
    public async Task<IActionResult> ListPrescriptions([FromQuery] HerbalistFilter f, CancellationToken ct) =>
        RequisitionResponse(await requisitionService.ListPrescriptionsAsync(f, ct));

    [HttpGet("prescriptions/{id:int}")]
    public async Task<IActionResult> Prescription(int id, CancellationToken ct) =>
        RequisitionResponse(await requisitionService.PrescriptionAsync(id, ct));

    [HttpGet("history")]
    public async Task<IActionResult> History([FromQuery] HerbalistFilter f, CancellationToken ct) =>
        RequisitionResponse(await requisitionService.HistoryAsync(f, ct));

    [HttpGet("lookups")]
    public async Task<IActionResult> Lookups(CancellationToken ct) =>
        RequisitionResponse(await requisitionService.LookupsAsync(ct));

    [HttpGet("requests")]
    public async Task<IActionResult> Requests([FromQuery] HerbalistFilter f, CancellationToken ct) =>
        RequisitionResponse(await requisitionService.RequestsAsync(f, ct));

    [HttpGet("requests/{id:int}")]
    public async Task<IActionResult> RequestDetail(int id, CancellationToken ct) =>
        RequisitionResponse(await requisitionService.RequestDetailAsync(id, ct));

}

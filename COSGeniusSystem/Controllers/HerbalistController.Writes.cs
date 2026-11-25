using COSGeniusSystem.Services.Herbalist;
using System.Data;
using COSGeniusSystem.DTOs;
using COSGeniusSystem.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace COSGeniusSystem.Controllers;

public partial class HerbalistController
{
    private IActionResult RequisitionResponse(RequisitionResult result) => result.Outcome switch
    {
        RequisitionOutcome.Success => Data(result.Data),
        RequisitionOutcome.NotFound => Failure(404, result.Message!),
        RequisitionOutcome.Invalid => Failure(400, result.Message!),
        RequisitionOutcome.Conflict => Failure(409, result.Message!),
        RequisitionOutcome.Unprocessable => Failure(422, result.Message!),
        RequisitionOutcome.SchemaUnavailable => Failure(503, result.Message!),
        _ => throw new InvalidOperationException("Unknown requisition outcome.")
    };

    [HttpPost("requests/preview")]
    public async Task<IActionResult> Preview(HerbalistSelection input, CancellationToken ct) =>
        RequisitionResponse(await requisitionService.PreviewAsync(input, ct));

    [HttpPost("requests")]
    public async Task<IActionResult> Create(HerbalistCreateRequest input, CancellationToken ct) =>
        RequisitionResponse(await requisitionService.CreateAsync(input, Actor, ct));

    [HttpPost("requests/{id:int}/receive")]
    public async Task<IActionResult> Receive(int id, ReceiveHerbs input, CancellationToken ct) =>
        RequisitionResponse(await requisitionService.ReceiveAsync(id, input, Actor, ct));

    [HttpPost("prescriptions/{id:int}/complete")]
    public async Task<IActionResult> Complete(int id, CompleteHerbs input, CancellationToken ct) =>
        RequisitionResponse(await requisitionService.CompleteAsync(id, input, Actor, ct));

    [HttpPost("requests/{id:int}/cancel")]
    public async Task<IActionResult> Cancel(int id, HerbalistVersion input, CancellationToken ct) =>
        RequisitionResponse(await requisitionService.CancelAsync(id, input, Actor, ct));

    [HttpGet("discrepancies")]
    public async Task<IActionResult> Discrepancies([FromQuery] HerbalistFilter f,CancellationToken ct) =>
        RequisitionResponse(await requisitionService.DiscrepanciesAsync(f, ct));
    [HttpGet("discrepancies/{id:int}")]
    public async Task<IActionResult> Discrepancy(int id,CancellationToken ct) =>
        RequisitionResponse(await requisitionService.DiscrepancyAsync(id, ct));
    [HttpPost("outbounds/{outboundId:int}/discrepancy-report")]
    public async Task<IActionResult> CreateDiscrepancy(int outboundId, ReceiveHerbs input, CancellationToken ct) =>
        RequisitionResponse(await requisitionService.CreateDiscrepancyAsync(outboundId, input, Actor, ct));

    [HttpGet("prescriptions/{id:int}/payment-eligibility")]
    public async Task<IActionResult> PaymentEligibility(int id,CancellationToken ct) =>
        RequisitionResponse(await requisitionService.PaymentEligibilityAsync(id, ct));
}

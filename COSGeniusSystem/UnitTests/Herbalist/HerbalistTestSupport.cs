using System;
using System.Collections.Generic;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using System.Linq;
using COSGeniusSystem.Commons;
using COSGeniusSystem.Controllers;
using COSGeniusSystem.Data;
using COSGeniusSystem.DTOs;
using COSGeniusSystem.Models;
using COSGeniusSystem.Services.Herbalist;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Moq;
using Xunit;

namespace COSGeniusSystem.UnitTests;

// Shared setup only; no Fact methods and no database seeding.
internal static class HerbalistTestSupport
{
    internal const string Version = "AAAAAAAAAAA=";
    internal static readonly CancellationToken Ct = CancellationToken.None;
    internal static ExceptionContext ErrorContext(Exception exception) => new(
        new ActionContext(new DefaultHttpContext(), new RouteData(), new ActionDescriptor()),
        new List<IFilterMetadata>()) { Exception = exception };
    internal static ApplicationDbContext ModelDb() => new(
        new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseSqlServer("Server=localhost;Database=SchemaChecks;Integrated Security=true;TrustServerCertificate=true").Options);

    internal static async Task CheckControllerAsync(string action, RequisitionOutcome outcome, int status)
    {
        var filter = new HerbalistFilter { PageNumber = 1, PageSize = 10 };
        var expected = new RequisitionResult(outcome, "Test message", new { marker = "test-payload" });
        var mock = new Mock<IHerbalistRequisitionService>(MockBehavior.Strict);
        var controller = new HerbalistController(mock.Object) {
            ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() }
        };
        controller.ControllerContext.HttpContext.User = new ClaimsPrincipal(new ClaimsIdentity(new[] {
            new Claim(ClaimTypes.NameIdentifier, "7"), new Claim(ClaimTypes.Role, "HERBALIST")
        }, "test"));
        IActionResult result;
        switch (action)
        {
            case "Capabilities":
                mock.Setup(x => x.CapabilitiesAsync(Ct)).ReturnsAsync(expected);
                result = await controller.Capabilities(Ct);
                break;
            case "Lookups":
                mock.Setup(x => x.LookupsAsync(Ct)).ReturnsAsync(expected);
                result = await controller.Lookups(Ct);
                break;
            case "ListPrescriptions":
                mock.Setup(x => x.ListPrescriptionsAsync(filter, Ct)).ReturnsAsync(expected);
                result = await controller.ListPrescriptions(filter, Ct);
                break;
            case "History":
                mock.Setup(x => x.HistoryAsync(filter, Ct)).ReturnsAsync(expected);
                result = await controller.History(filter, Ct);
                break;
            case "Requests":
                mock.Setup(x => x.RequestsAsync(filter, Ct)).ReturnsAsync(expected);
                result = await controller.Requests(filter, Ct);
                break;
            case "Patients":
                mock.Setup(x => x.PatientsAsync(filter, Ct)).ReturnsAsync(expected);
                result = await controller.Patients(filter, Ct);
                break;
            case "Herbs":
                mock.Setup(x => x.HerbsAsync(filter, Ct)).ReturnsAsync(expected);
                result = await controller.Herbs(filter, Ct);
                break;
            case "Formulas":
                mock.Setup(x => x.FormulasAsync(filter, Ct)).ReturnsAsync(expected);
                result = await controller.Formulas(filter, Ct);
                break;
            case "Discrepancies":
                mock.Setup(x => x.DiscrepanciesAsync(filter, Ct)).ReturnsAsync(expected);
                result = await controller.Discrepancies(filter, Ct);
                break;
            case "Prescription":
                mock.Setup(x => x.PrescriptionAsync(11, Ct)).ReturnsAsync(expected);
                result = await controller.Prescription(11, Ct);
                break;
            case "RequestDetail":
                mock.Setup(x => x.RequestDetailAsync(11, Ct)).ReturnsAsync(expected);
                result = await controller.RequestDetail(11, Ct);
                break;
            case "PatientDetail":
                mock.Setup(x => x.PatientDetailAsync(11, Ct)).ReturnsAsync(expected);
                result = await controller.PatientDetail(11, Ct);
                break;
            case "HerbDetail":
                mock.Setup(x => x.HerbDetailAsync(11, Ct)).ReturnsAsync(expected);
                result = await controller.HerbDetail(11, Ct);
                break;
            case "FormulaDetail":
                mock.Setup(x => x.FormulaDetailAsync(11, Ct)).ReturnsAsync(expected);
                result = await controller.FormulaDetail(11, Ct);
                break;
            case "Discrepancy":
                mock.Setup(x => x.DiscrepancyAsync(11, Ct)).ReturnsAsync(expected);
                result = await controller.Discrepancy(11, Ct);
                break;
            case "PaymentEligibility":
                mock.Setup(x => x.PaymentEligibilityAsync(11, Ct)).ReturnsAsync(expected);
                result = await controller.PaymentEligibility(11, Ct);
                break;
            case "Preview":
                mock.Setup(x => x.PreviewAsync(It.Is<HerbalistSelection>(x => x.WarehouseId == 1 && x.PrescriptionIds.Length == 1 && x.PrescriptionIds[0] == 11), Ct)).ReturnsAsync(expected);
                result = await controller.Preview(new HerbalistSelection(new[] { 11 }, 1), Ct);
                break;
            case "Create":
                mock.Setup(x => x.CreateAsync(It.Is<HerbalistCreateRequest>(x => x.WarehouseId == 1 && x.PrescriptionIds.Length == 1 && x.PrescriptionIds[0] == 11 && x.Note == "Note"), 7, Ct)).ReturnsAsync(expected);
                result = await controller.Create(new HerbalistCreateRequest(new[] { 11 }, "Note", 1), Ct);
                break;
            case "Receive":
                mock.Setup(x => x.ReceiveAsync(11, It.Is<ReceiveHerbs>(x => x.RowVersion == Version && x.Lines.Length == 1 && x.Lines[0].OutboundDetailId == 8 && x.Lines[0].ReceiveFull && x.Lines[0].ReceivedQuantity == null), 7, Ct)).ReturnsAsync(expected);
                result = await controller.Receive(11, new ReceiveHerbs(Version, new[] { new ReceiptLine(8, null, true) }, null), Ct);
                break;
            case "Complete":
                mock.Setup(x => x.CompleteAsync(11, It.Is<CompleteHerbs>(x => x.RowVersion == Version && x.Allocations.Length == 1 && x.Allocations[0].OutboundDetailId == 8 && x.Allocations[0].QuantityGrams == 10m), 7, Ct)).ReturnsAsync(expected);
                result = await controller.Complete(11, new CompleteHerbs(Version, new[] { new DispenseAllocation(8, 10m) }), Ct);
                break;
            case "Cancel":
                mock.Setup(x => x.CancelAsync(11, It.Is<HerbalistVersion>(x => x.RowVersion == Version), 7, Ct)).ReturnsAsync(expected);
                result = await controller.Cancel(11, new HerbalistVersion(Version), Ct);
                break;
            case "CreateDiscrepancy":
                mock.Setup(x => x.CreateDiscrepancyAsync(11, It.Is<ReceiveHerbs>(x => x.RowVersion == Version && x.Reason == "Short by 1g" && x.Lines.Length == 1 && x.Lines[0].OutboundDetailId == 8 && x.Lines[0].ReceivedQuantity == 9m && !x.Lines[0].ReceiveFull), 7, Ct)).ReturnsAsync(expected);
                result = await controller.CreateDiscrepancy(11, new ReceiveHerbs(Version, new[] { new ReceiptLine(8, 9m) }, "Short by 1g"), Ct);
                break;
            default: throw new ArgumentException("Unknown test action", nameof(action));
        }
        var response = Assert.IsAssignableFrom<ObjectResult>(result);
        Assert.Equal(status, response.StatusCode);
        Assert.IsType<ApiResponseModel<object>>(response.Value);
        // One expected call, with the requested actor/IDs/DTO values/token above.
        mock.VerifyAll();
        Assert.Single(mock.Invocations);
        mock.VerifyNoOtherCalls();
    }

}

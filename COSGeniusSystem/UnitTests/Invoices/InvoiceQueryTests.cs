using System;
using System.Linq;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Reflection;
using System.Security.Claims;
using System.Text.Json;
using System.Threading.Tasks;
using COSGeniusSystem.Commons;
using COSGeniusSystem.Controllers;
using COSGeniusSystem.Data;
using COSGeniusSystem.DTOs;
using COSGeniusSystem.Exceptions;
using COSGeniusSystem.Models;
using COSGeniusSystem.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Moq;
using Xunit;
using static COSGeniusSystem.UnitTests.InvoiceTestSupport;

namespace COSGeniusSystem.UnitTests;

public class InvoiceQueryTests
{
    // UTC-INV-001
    [Fact]
    [Trait("Scope", "RealService")]
    public async Task GetInvoicesAsync_FiltersStatusAndKeyword()
    {
        using var s = await Scope.CreateAsync(); await s.InvoiceAsync(1); await s.InvoiceAsync(2,"PAID");
        var result = Rows(await s.Api.GetInvoicesAsync(new() { PaymentStatus = "UNPAID", Keyword = " HD001 " }));
        Assert.Equal(1, Assert.Single(result).InvoiceId);
    }

    // UTC-INV-002
    [Fact]
    [Trait("Scope", "RealService")]
    public async Task GetInvoicesAsync_OrdersAndPaginatesNewestFirst()
    {
        using var s = await Scope.CreateAsync(); var now = DateTime.UtcNow;
        await s.InvoiceAsync(1, issued: now.AddMinutes(-2)); await s.InvoiceAsync(2, issued: now.AddMinutes(-1)); await s.InvoiceAsync(3, issued: now);
        var result = Rows(await s.Api.GetInvoicesAsync(new() { Page = 2, PageSize = 1 }));
        Assert.Equal(2, Assert.Single(result).InvoiceId);
    }

    // UTC-INV-003
    [Fact]
    [Trait("Scope", "RealService")]
    public async Task GetInvoicesAsync_InvalidDateRangeIsRejected()
    {
        using var s = await Scope.CreateAsync();
        await Assert.ThrowsAsync<BadRequestException>(() => s.Api.GetInvoicesAsync(new() { FromDate = DateTime.Today.AddDays(1), ToDate = DateTime.Today }));
    }

    // UTC-INV-004
    [Fact]
    [Trait("Scope", "RealService")]
    public async Task GetInvoicesAsync_UnsupportedStatusIsRejected()
    {
        using var s = await Scope.CreateAsync();
        await Assert.ThrowsAsync<BadRequestException>(() => s.Api.GetInvoicesAsync(new() { PaymentStatus = "UNKNOWN" }));
    }

    // UTC-INV-005
    [Fact]
    [Trait("Scope", "RealService")]
    public async Task GetInvoicesAsync_InvalidPagingIsClamped()
    {
        using var s = await Scope.CreateAsync(); await s.InvoiceAsync();
        Assert.Single(Rows(await s.Api.GetInvoicesAsync(new() { Page = 0, PageSize = 0 })));
    }

    // UTC-INV-006
    [Fact]
    [Trait("Scope", "RealService")]
    public async Task GetInvoiceByIdAsync_KeepsPrescriptionAndServiceReferencesSeparate()
    {
        using var s = await Scope.CreateAsync(); var rx = await s.PrescriptionAsync(); var detail = await s.PlanAsync(); var invoice = await s.InvoiceAsync();
        invoice.InvoiceDetails.Add(new() { ItemType = "HERB_REMEDY", ItemName = "Formula", Prescription = rx, PrescriptionId = rx.PrescriptionId, Quantity = 1, UnitPrice = 10000m });
        invoice.InvoiceDetails.Add(new() { ItemType = "SERVICE", ItemName = "Acupuncture", PlanDetail = detail, PlanDetailId = detail.PlanDetailId, Quantity = 1, UnitPrice = 100000m });
        await s.Db.SaveChangesAsync();
        var result = await s.Api.GetInvoiceByIdAsync(1);
        var medicine = Assert.Single(result.Items.Where(x => x.ItemType == "HERB_REMEDY"));
        Assert.Equal(1, medicine.PrescriptionId); Assert.Null(medicine.PlanDetailId);
        var service = Assert.Single(result.Items.Where(x => x.ItemType == "SERVICE"));
        Assert.Equal(1, service.PlanDetailId); Assert.Null(service.PrescriptionId); Assert.Equal(1, service.PlanId);
    }

    // UTC-INV-007
    [Fact]
    [Trait("Scope", "RealService")]
    public async Task GetInvoiceByIdAsync_MissingInvoiceIsRejected()
    {
        using var s = await Scope.CreateAsync(); await Assert.ThrowsAsync<NotFoundException>(() => s.Api.GetInvoiceByIdAsync(999));
    }

    // UTC-INV-008
    [Fact]
    [Trait("Scope", "RealService")]
    public async Task GetInvoiceByIdAsync_UsesStoredSnapshots()
    {
        using var s = await Scope.CreateAsync(); var invoice = await s.InvoiceAsync();
        invoice.PatientNameSnapshot = "Patient original"; invoice.PatientCodeSnapshot = "BN-OLD";
        invoice.PhoneSnapshot = "0987654321"; invoice.DoctorNameSnapshot = "Doctor original";
        invoice.CashierNameSnapshot = "Cashier original"; invoice.ExamDateSnapshot = new DateTime(2026,1,1);
        await s.Db.SaveChangesAsync(); var result = await s.Api.GetInvoiceByIdAsync(1);
        Assert.Equal("Patient original", result.PatientName); Assert.Equal("BN-OLD",result.PatientCode);
        Assert.Equal("0987654321",result.Phone); Assert.Equal("Doctor original",result.DoctorName);
        Assert.Equal("Cashier original",result.CashierName); Assert.Equal(new DateTime(2026,1,1),result.ExamDate);
    }

    // UTC-INV-009
    [Fact]
    [Trait("Scope", "RealService")]
    public async Task GetInvoiceByIdAsync_RestoresIngredientsAndCalculatesMissingSubtotal()
    {
        using var s = await Scope.CreateAsync(); var rx = await s.PrescriptionAsync(); var invoice = await s.InvoiceAsync();
        invoice.InvoiceDetails.Add(new() { ItemType = "HERB_REMEDY", ItemName = "Formula", Prescription = rx,
            Quantity = 2, UnitPrice = 100m, SubTotal = null,
            IngredientsJson = JsonSerializer.Serialize(new[] { new InvoiceIngredient("Herb",10m,5,50m,"g",200m,10000m) }) });
        await s.Db.SaveChangesAsync(); var item = Assert.Single((await s.Api.GetInvoiceByIdAsync(1)).Items);
        Assert.Equal(200m,item.SubTotal); Assert.Equal(50m,Assert.Single(item.Ingredients).Quantity);
    }

    // UTC-INV-010
    [Fact]
    [Trait("Scope", "RealService")]
    public async Task GetInvoiceByIdAsync_CancelledInvoiceRemainsAvailable()
    {
        using var s = await Scope.CreateAsync(); var invoice = await s.InvoiceAsync(status:"CANCELLED");
        invoice.CancellationReason = "Patient changed payment selection"; invoice.CancelledBy = 1; invoice.CancelledAt = DateTime.UtcNow;
        await s.Db.SaveChangesAsync(); var result = await s.Api.GetInvoiceByIdAsync(1);
        Assert.Equal("CANCELLED",result.PaymentStatus); Assert.Equal(invoice.CancellationReason,result.CancellationReason);
    }

    // UTC-INV-011
    [Fact]
    [Trait("Scope", "ControllerMock")]
    public async Task GetInvoices_ForwardsQueryAndReturns200()
    {
        var mock = new Mock<IInvoiceService>(MockBehavior.Strict); var query = new InvoiceQueryParams { PaymentStatus = "UNPAID" };
        mock.Setup(x=>x.GetInvoicesAsync(query)).ReturnsAsync(new PaginatedResult<InvoiceListDto>(new List<InvoiceListDto>(),0,1,10));
        var response = await Controller(mock.Object,Actor()).GetInvoices(query);
        Assert.IsType<ApiResponseModel<PaginatedResult<InvoiceListDto>>>(Assert.IsType<OkObjectResult>(response.Result).Value); mock.VerifyAll();
    }

    // UTC-INV-012
    [Fact]
    [Trait("Scope", "ControllerMock")]
    public async Task GetInvoices_PropagatesInvalidQuery()
    {
        var mock = new Mock<IInvoiceService>(MockBehavior.Strict); var query = new InvoiceQueryParams();
        mock.Setup(x=>x.GetInvoicesAsync(query)).ThrowsAsync(new BadRequestException("Service error"));
        await Assert.ThrowsAsync<BadRequestException>(()=>Controller(mock.Object,Actor()).GetInvoices(query)); mock.VerifyAll();
    }

    // UTC-INV-013
    [Fact]
    [Trait("Scope", "ControllerMock")]
    public async Task GetInvoices_PropagatesServiceConflict()
    {
        var mock = new Mock<IInvoiceService>(MockBehavior.Strict); var query = new InvoiceQueryParams();
        mock.Setup(x=>x.GetInvoicesAsync(query)).ThrowsAsync(new ConflictException("Service error"));
        await Assert.ThrowsAsync<ConflictException>(()=>Controller(mock.Object,Actor()).GetInvoices(query)); mock.VerifyAll();
    }

    // UTC-INV-014
    [Fact]
    [Trait("Scope", "ControllerMock")]
    public async Task GetInvoiceById_ForwardsIdAndReturns200()
    {
        var mock = new Mock<IInvoiceService>(MockBehavior.Strict);
        mock.Setup(x=>x.GetInvoiceByIdAsync(1)).ReturnsAsync(new InvoiceDetailDto { InvoiceId=1 });
        var response = await Controller(mock.Object,Actor()).GetInvoiceById(1);
        Assert.IsType<ApiResponseModel<InvoiceDetailDto>>(Assert.IsType<OkObjectResult>(response.Result).Value); mock.VerifyAll();
    }

    // UTC-INV-015
    [Fact]
    [Trait("Scope", "ControllerMock")]
    public async Task GetInvoiceById_PropagatesMissingInvoice()
    {
        var mock = new Mock<IInvoiceService>(MockBehavior.Strict);
        mock.Setup(x=>x.GetInvoiceByIdAsync(1)).ThrowsAsync(new NotFoundException("Service error"));
        await Assert.ThrowsAsync<NotFoundException>(()=>Controller(mock.Object,Actor()).GetInvoiceById(1)); mock.VerifyAll();
    }

    // UTC-INV-016
    [Fact]
    [Trait("Scope", "ControllerMock")]
    public async Task GetInvoiceById_PropagatesServiceConflict()
    {
        var mock = new Mock<IInvoiceService>(MockBehavior.Strict);
        mock.Setup(x=>x.GetInvoiceByIdAsync(1)).ThrowsAsync(new ConflictException("Service error"));
        await Assert.ThrowsAsync<ConflictException>(()=>Controller(mock.Object,Actor()).GetInvoiceById(1)); mock.VerifyAll();
    }

    // UTC-INV-017
    [Fact]
    [Trait("Scope", "ControllerMock")]
    public void Methods_ReturnsSupportedPaymentMethodsWithoutServiceCall()
    {
        var mock = new Mock<IInvoiceService>(MockBehavior.Strict);
        var response = Assert.IsType<OkObjectResult>(Controller(mock.Object,Actor()).Methods());
        Assert.IsType<ApiResponseModel<string[]>>(response.Value);
        Assert.Equal(new[] {"Tiền mặt","Chuyển khoản"},InvoiceService.PaymentMethods); mock.VerifyNoOtherCalls();
    }
}

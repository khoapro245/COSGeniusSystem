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

public class InvoicePaymentTests
{
    // UTC-INV-027
    [Fact]
    [Trait("Scope", "ServicePreflight")]
    public async Task PayAsync_UnauthorizedRolesAreRejectedBeforeDatabaseAccess()
    {
        using var s=await Scope.CreateAsync();
        await Assert.ThrowsAsync<ForbiddenException>(()=>s.Api.PayAsync(Actor("DOCTOR"),1,Payment()));
        await Assert.ThrowsAsync<ForbiddenException>(()=>s.Api.PayAsync(Actor("HERBALIST"),1,Payment()));
    }

    // UTC-INV-028
    [Fact]
    [Trait("Scope", "ServicePreflight")]
    public async Task PayAsync_InvalidActorIdIsRejectedBeforeDatabaseAccess()
    {
        using var s=await Scope.CreateAsync();
        await Assert.ThrowsAsync<UnauthorizedException>(()=>s.Api.PayAsync(Actor(id:"0"),1,Payment()));
    }

    // UTC-INV-029
    [Fact]
    [Trait("Scope", "ServicePreflight")]
    public async Task PayAsync_UnconfirmedReceiptIsRejectedBeforeDatabaseAccess()
    {
        using var s=await Scope.CreateAsync(); var request=Payment();request.ReceivedInFull=false;request.PaymentMethod="Chuyển khoản";
        await Assert.ThrowsAsync<BadRequestException>(()=>s.Api.PayAsync(Actor(),1,request));
    }

    // UTC-INV-030
    [Fact]
    [Trait("Scope", "ServicePreflight")]
    public async Task PayAsync_UnsupportedMethodIsRejectedBeforeDatabaseAccess()
    {
        using var s=await Scope.CreateAsync(); var request=Payment();request.PaymentMethod="UNKNOWN";
        await Assert.ThrowsAsync<BadRequestException>(()=>s.Api.PayAsync(Actor(),1,request));
    }

    // UTC-INV-031
    [Fact]
    [Trait("Scope", "ServicePreflight")]
    public async Task PayAsync_AdminReachesPaymentValidation()
    {
        using var s=await Scope.CreateAsync(); var request=Payment();request.ReceivedInFull=false;
        await Assert.ThrowsAsync<BadRequestException>(()=>s.Api.PayAsync(Actor("ADMIN"),1,request));
    }

    // UTC-INV-032
    [Fact]
    [Trait("Scope", "ControllerMock")]
    public async Task Pay_ForwardsWholeInvoicePayment()
    {
        var mock=new Mock<IInvoiceService>(MockBehavior.Strict);var actor=Actor();var request=Payment();request.InvoiceDetailIds=null;
        mock.Setup(x=>x.PayAsync(actor,1,request)).ReturnsAsync(new InvoiceDetailDto {InvoiceId=1,PaymentStatus="PAID"});
        var response=Assert.IsType<OkObjectResult>(await Controller(mock.Object,actor).Pay(1,request));
        Assert.IsType<ApiResponseModel<InvoiceDetailDto>>(response.Value);mock.VerifyAll();Assert.Single(mock.Invocations);
    }

    // UTC-INV-033
    [Fact]
    [Trait("Scope", "ControllerMock")]
    public async Task Pay_ForwardsSelectedInvoiceItems()
    {
        var mock=new Mock<IInvoiceService>(MockBehavior.Strict);var actor=Actor();var request=Payment();request.InvoiceDetailIds=new List<int> { 10 };
        mock.Setup(x=>x.PayAsync(actor,1,request)).ReturnsAsync(new InvoiceDetailDto {InvoiceId=1,PaymentStatus="PAID"});
        var response=Assert.IsType<OkObjectResult>(await Controller(mock.Object,actor).Pay(1,request));
        Assert.IsType<ApiResponseModel<InvoiceDetailDto>>(response.Value);mock.VerifyAll();Assert.Single(mock.Invocations);
    }

    // UTC-INV-034
    [Fact]
    [Trait("Scope", "ControllerMock")]
    public async Task Pay_PropagatesPaymentConflict()
    {
        var mock=new Mock<IInvoiceService>(MockBehavior.Strict);var actor=Actor();var request=Payment();
        mock.Setup(x=>x.PayAsync(actor,1,request)).ThrowsAsync(new ConflictException("Already paid or stale"));
        await Assert.ThrowsAsync<ConflictException>(()=>Controller(mock.Object,actor).Pay(1,request));mock.VerifyAll();
    }

    // UTC-INV-035
    [Fact]
    [Trait("Scope", "DtoValidation")]
    public void PaymentDto_ValidPaymentContract()
    {
        var request=Payment();
        Assert.True(Valid(request));
    }

    // UTC-INV-036
    [Fact]
    [Trait("Scope", "DtoValidation")]
    public void PaymentDto_InvalidVersionLength()
    {
        var request=Payment();
        request.RowVersion=new byte[7];
        Assert.False(Valid(request));
    }

    // UTC-INV-037
    [Fact]
    [Trait("Scope", "DtoValidation")]
    public void PaymentDto_MissingPaymentMethod()
    {
        var request=Payment();
        request.PaymentMethod="";
        Assert.False(Valid(request));
    }
}

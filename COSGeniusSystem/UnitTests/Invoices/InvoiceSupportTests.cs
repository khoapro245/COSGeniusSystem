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

public class InvoiceSupportTests
{
    // UTC-INV-047
    [Fact]
    [Trait("Scope", "ControllerMock")]
    public async Task Backfill_ReturnsProcessedRecordCount()
    {
        var mock=new Mock<IInvoiceService>(MockBehavior.Strict);mock.Setup(x=>x.BackfillAsync()).ReturnsAsync(3);
        var response=Assert.IsType<OkObjectResult>(await Controller(mock.Object,Actor("ADMIN")).Backfill());
        Assert.IsType<ApiResponseModel<int>>(response.Value);mock.VerifyAll();Assert.Single(mock.Invocations);
    }

    // UTC-INV-048
    [Fact]
    [Trait("Scope", "ControllerMock")]
    public async Task Backfill_HandlesNoRecords()
    {
        var mock=new Mock<IInvoiceService>(MockBehavior.Strict);mock.Setup(x=>x.BackfillAsync()).ReturnsAsync(0);
        var response=Assert.IsType<OkObjectResult>(await Controller(mock.Object,Actor("ADMIN")).Backfill());
        Assert.IsType<ApiResponseModel<int>>(response.Value);mock.VerifyAll();Assert.Single(mock.Invocations);
    }

    // UTC-INV-049
    [Fact]
    [Trait("Scope", "ControllerMock")]
    public async Task Backfill_PropagatesAccrualFailure()
    {
        var mock=new Mock<IInvoiceService>(MockBehavior.Strict);mock.Setup(x=>x.BackfillAsync()).ThrowsAsync(new ConflictException("Invalid charge"));
        await Assert.ThrowsAsync<ConflictException>(()=>Controller(mock.Object,Actor("ADMIN")).Backfill());mock.VerifyAll();
    }

    // UTC-INV-050
    [Fact]
    [Trait("Scope", "Metadata")]
    public void AuthorizationMetadata_InvoiceControllerRequiresCashierOrAdmin()
    {
        Assert.Equal("CASHIER,ADMIN",typeof(InvoicesController).GetCustomAttribute<AuthorizeAttribute>()!.Roles);
    }

    // UTC-INV-051
    [Fact]
    [Trait("Scope", "Metadata")]
    public void AuthorizationMetadata_BackfillRequiresAdmin()
    {
        Assert.Equal("ADMIN",typeof(InvoicesController).GetMethod("Backfill")!.GetCustomAttribute<AuthorizeAttribute>()!.Roles);
    }

    // UTC-INV-052
    [Fact]
    [Trait("Scope", "DtoValidation")]
    public void PaymentContract_DoesNotAcceptClientPrices()
    {
        var request=JsonSerializer.Deserialize<InvoicePaymentRequest>("""
        {"PaymentMethod":"Tiền mặt","ReceivedInFull":true,"TotalAmount":1,"UnitPrice":1,"Discount":999999}
        """)!;
        Assert.Null(typeof(InvoicePaymentRequest).GetProperty("TotalAmount"));
        Assert.Null(typeof(InvoicePaymentRequest).GetProperty("UnitPrice"));Assert.Null(typeof(InvoicePaymentRequest).GetProperty("Discount"));
        Assert.False(Valid(request));request.RowVersion=new byte[8];Assert.True(Valid(request));
    }
}

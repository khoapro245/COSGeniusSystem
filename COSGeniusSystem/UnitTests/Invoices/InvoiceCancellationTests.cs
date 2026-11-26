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

public class InvoiceCancellationTests
{
    // UTC-INV-038
    [Fact]
    [Trait("Scope", "ServicePreflight")]
    public async Task CancelAsync_UnauthorizedActorIsRejectedBeforeDatabaseAccess()
    {
        using var s=await Scope.CreateAsync();
        await Assert.ThrowsAsync<ForbiddenException>(()=>s.Api.CancelAsync(Actor("DOCTOR"),1,new(){RowVersion=new byte[8],Reason="Cancel"}));
    }

    // UTC-INV-039
    [Fact]
    [Trait("Scope", "ServicePreflight")]
    public async Task CancelAsync_MissingReasonIsRejected()
    {
        using var s=await Scope.CreateAsync();
        await Assert.ThrowsAsync<BadRequestException>(()=>s.Api.CancelAsync(Actor(),1,new(){RowVersion=new byte[8],Reason=""}));
    }

    // UTC-INV-040
    [Fact]
    [Trait("Scope", "ServicePreflight")]
    public async Task CancelAsync_OverlongReasonIsRejected()
    {
        using var s=await Scope.CreateAsync();
        await Assert.ThrowsAsync<BadRequestException>(()=>s.Api.CancelAsync(Actor(),1,new(){RowVersion=new byte[8],Reason=new string('x',501)}));
    }

    // UTC-INV-041
    [Fact]
    [Trait("Scope", "ControllerMock")]
    public async Task Cancel_ForwardsReasonVersionAndActor()
    {
        var mock=new Mock<IInvoiceService>(MockBehavior.Strict);var actor=Actor();
        var request=new InvoiceCancelRequest{RowVersion=new byte[8],Reason="Patient changed payment selection"};
        mock.Setup(x=>x.CancelAsync(actor,1,request)).ReturnsAsync(new InvoiceDetailDto{InvoiceId=1,PaymentStatus="CANCELLED"});
        Assert.IsType<OkObjectResult>(await Controller(mock.Object,actor).Cancel(1,request));mock.VerifyAll();Assert.Single(mock.Invocations);
    }

    // UTC-INV-042
    [Fact]
    [Trait("Scope", "ControllerMock")]
    public async Task Cancel_PropagatesPaidOrStaleInvoiceConflict()
    {
        var mock=new Mock<IInvoiceService>(MockBehavior.Strict);var actor=Actor();var request=new InvoiceCancelRequest{RowVersion=new byte[8],Reason="Cancel"};
        mock.Setup(x=>x.CancelAsync(actor,1,request)).ThrowsAsync(new ConflictException("Service error"));
        await Assert.ThrowsAsync<ConflictException>(()=>Controller(mock.Object,actor).Cancel(1,request));mock.VerifyAll();
    }

    // UTC-INV-043
    [Fact]
    [Trait("Scope", "ControllerMock")]
    public async Task Cancel_PropagatesMissingInvoice()
    {
        var mock=new Mock<IInvoiceService>(MockBehavior.Strict);var actor=Actor();var request=new InvoiceCancelRequest{RowVersion=new byte[8],Reason="Cancel"};
        mock.Setup(x=>x.CancelAsync(actor,1,request)).ThrowsAsync(new NotFoundException("Service error"));
        await Assert.ThrowsAsync<NotFoundException>(()=>Controller(mock.Object,actor).Cancel(1,request));mock.VerifyAll();
    }

    // UTC-INV-044
    [Fact]
    [Trait("Scope", "DtoValidation")]
    public void CancellationDto_ValidCancellationContract()
    {
        var request=new InvoiceCancelRequest{RowVersion=new byte[8],Reason="Cancel"};
        Assert.True(Valid(request));
    }

    // UTC-INV-045
    [Fact]
    [Trait("Scope", "DtoValidation")]
    public void CancellationDto_InvalidVersionLength()
    {
        var request=new InvoiceCancelRequest{RowVersion=new byte[8],Reason="Cancel"};
        request.RowVersion=new byte[9];
        Assert.False(Valid(request));
    }

    // UTC-INV-046
    [Fact]
    [Trait("Scope", "DtoValidation")]
    public void CancellationDto_MissingCancellationReason()
    {
        var request=new InvoiceCancelRequest{RowVersion=new byte[8],Reason="Cancel"};
        request.Reason="";
        Assert.False(Valid(request));
    }
}

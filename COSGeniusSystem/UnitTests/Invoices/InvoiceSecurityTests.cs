using COSGeniusSystem.Controllers;
using COSGeniusSystem.DTOs;
using COSGeniusSystem.Exceptions;
using COSGeniusSystem.Services;
using Microsoft.AspNetCore.Authorization;
using System.ComponentModel.DataAnnotations;
using System.Reflection;
using System.Security.Claims;
using System.Text.Json;
namespace COSGeniusSystem.UnitTests;
public class InvoiceSecurityTests
{
    private static ClaimsPrincipal Actor(string role) => new(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, "1"), new Claim(ClaimTypes.Role, role)], "Test"));
    [Fact]
    public async Task OnlyCashierOrAdminCanCollect_AndTransferRequiresReceivedConfirmation()
    {
        using var db = SchemaFixture.Create(); var service = new InvoiceService(db);
        await Assert.ThrowsAsync<ForbiddenException>(() => service.PayAsync(Actor("DOCTOR"), 1, new()));
        await Assert.ThrowsAsync<ForbiddenException>(() => service.PayAsync(Actor("HERBALIST"), 1, new()));
        await Assert.ThrowsAsync<BadRequestException>(() => service.PayAsync(Actor("CASHIER"), 1,
            new() { PaymentMethod = "Chuyển khoản", ReceivedInFull = false }));
        await Assert.ThrowsAsync<BadRequestException>(() => service.PayAsync(Actor("CASHIER"), 1,
            new() { PaymentMethod = "UNKNOWN", ReceivedInFull = true }));
        Assert.Equal("CASHIER,ADMIN", typeof(InvoicesController).GetCustomAttribute<AuthorizeAttribute>()!.Roles);
        Assert.Equal("ADMIN", typeof(InvoicesController).GetMethod("Backfill")!.GetCustomAttribute<AuthorizeAttribute>()!.Roles);
    }
    [Fact]
    public void PaymentContractContainsNoClientPrices_AndRequiresEightByteVersion()
    {
        var request = JsonSerializer.Deserialize<InvoicePaymentRequest>("""
            {"PaymentMethod":"Tiền mặt","ReceivedInFull":true,"TotalAmount":1,"UnitPrice":1,"Discount":999999}
            """)!;
        Assert.Null(typeof(InvoicePaymentRequest).GetProperty("TotalAmount"));
        Assert.False(Validator.TryValidateObject(request, new ValidationContext(request), [], true));
        request.RowVersion = new byte[8];
        Assert.True(Validator.TryValidateObject(request, new ValidationContext(request), [], true));
    }
}

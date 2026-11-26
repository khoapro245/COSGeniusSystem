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

public class InvoiceChargeAccrualTests
{
    // UTC-INV-018
    [Fact]
    [Trait("Scope", "AccrualInMemory")]
    public async Task GenerateAsync_IncompleteExaminationCreatesNoDebt()
    {
        using var s = await Scope.CreateAsync(); await s.PrescriptionAsync(); await s.PlanAsync();
        s.Record.Status = "IN_PROGRESS"; await s.Db.SaveChangesAsync();
        await InvoiceAccrual.GenerateAsync(s.Db,1); Assert.False(await s.Db.Invoices.AnyAsync());
    }

    // UTC-INV-019
    [Fact]
    [Trait("Scope", "AccrualInMemory")]
    public async Task GenerateAsync_ConfirmedPrescriptionIsBilledBeforeDispensing()
    {
        using var s = await Scope.CreateAsync(); await s.PrescriptionAsync();
        Assert.False(await s.Db.MedicineDispenses.AnyAsync()); await InvoiceAccrual.GenerateAsync(s.Db,1);
        var invoice = await s.Db.Invoices.Include(x=>x.InvoiceDetails).SingleAsync();
        Assert.Equal("UNPAID",invoice.PaymentStatus); Assert.Equal(10000m,invoice.TotalAmount);
        var line = Assert.Single(invoice.InvoiceDetails); Assert.Equal("HERB_REMEDY",line.ItemType);
        Assert.Equal(1m,line.Quantity); Assert.Equal(10000m,line.UnitPrice);
        var ingredient = Assert.Single(JsonSerializer.Deserialize<List<InvoiceIngredient>>(line.IngredientsJson!)!);
        Assert.Equal(50m,ingredient.Quantity); Assert.Equal(200m,ingredient.UnitPrice);
    }

    // UTC-INV-020
    [Fact]
    [Trait("Scope", "AccrualInMemory")]
    public async Task GenerateAsync_CourseAndSingleBillPrescribedQuantityBeforeExecution()
    {
        using var s = await Scope.CreateAsync(); await s.PlanAsync(); await s.PlanAsync(2,"SINGLE",1);
        Assert.False(await s.Db.UsageHistories.AnyAsync()); await InvoiceAccrual.GenerateAsync(s.Db,1);
        var invoice = await s.Db.Invoices.Include(x=>x.InvoiceDetails).SingleAsync();
        Assert.Equal(400000m,invoice.TotalAmount); Assert.Equal(2,invoice.InvoiceDetails.Count);
        Assert.Equal(3m,invoice.InvoiceDetails.Single(x=>x.TreatmentTypeSnapshot=="COURSE").Quantity);
        Assert.Equal(1m,invoice.InvoiceDetails.Single(x=>x.TreatmentTypeSnapshot=="SINGLE").Quantity);
    }

    // UTC-INV-021
    [Fact]
    [Trait("Scope", "AccrualInMemory")]
    public async Task GenerateAsync_RetryAndNewChargesPreservePaidInvoice()
    {
        using var s = await Scope.CreateAsync(); var rx = await s.PrescriptionAsync();
        await InvoiceAccrual.GenerateAsync(s.Db,1); await InvoiceAccrual.GenerateAsync(s.Db,1);
        var original = await s.Db.Invoices.SingleAsync(); Assert.Equal(1,await s.Db.Invoices.CountAsync());
        original.PaymentStatus="PAID"; rx.HerbalPrescriptionDetails.Single().Herb.StandardPrice=999999m;
        await s.Db.SaveChangesAsync(); await s.PlanAsync(); await InvoiceAccrual.GenerateAsync(s.Db,1);
        s.Db.ChangeTracker.Clear(); var saved=await s.Db.Invoices.Include(x=>x.InvoiceDetails).SingleAsync(x=>x.InvoiceId==original.InvoiceId);
        Assert.Equal("PAID",saved.PaymentStatus); Assert.Equal(10000m,saved.TotalAmount); Assert.Single(saved.InvoiceDetails);
        Assert.Equal(2,await s.Db.Invoices.CountAsync());
    }

    // UTC-INV-022
    [Fact]
    [Trait("Scope", "AccrualInMemory")]
    public async Task GenerateAsync_CancelledDebtReusesOriginalSnapshotPrice()
    {
        using var s = await Scope.CreateAsync(); var detail = await s.PlanAsync();
        var cancelled = await s.InvoiceAsync(status:"CANCELLED"); cancelled.TotalAmount=cancelled.FinalAmount=140000m;
        cancelled.InvoiceDetails.Add(new() { PlanDetail=detail, PlanDetailId=1, ItemType="SERVICE",ItemName="Original service",
            SourceKey="SERVICE:1:2",Quantity=2,UnitPrice=70000m,IsActive=false });
        await s.Db.SaveChangesAsync(); await InvoiceAccrual.GenerateAsync(s.Db,1); await InvoiceAccrual.GenerateAsync(s.Db,1);
        var replacement=await s.Db.Invoices.Include(x=>x.InvoiceDetails).SingleAsync(x=>x.PaymentStatus=="UNPAID");
        Assert.Equal(240000m,replacement.TotalAmount); Assert.Equal(2,replacement.InvoiceDetails.Count);
        var original=Assert.Single(replacement.InvoiceDetails.Where(x=>x.SourceKey=="SERVICE:1:2"));
        Assert.Equal("Original service",original.ItemName);Assert.Equal(2m,original.Quantity);Assert.Equal(70000m,original.UnitPrice);
        var additional=Assert.Single(replacement.InvoiceDetails.Where(x=>x.SourceKey!="SERVICE:1:2"));
        Assert.Equal(1m,additional.Quantity);Assert.Equal(100000m,additional.UnitPrice);
        Assert.Equal(2,await s.Db.Invoices.CountAsync());
    }

    // UTC-INV-023
    [Fact]
    [Trait("Scope", "Rules")]
    public void IsPaid_CanonicalPaidStatus()
    {
        Assert.Equal(true,InvoiceAccrual.IsPaid("PAID"));
    }

    // UTC-INV-024
    [Fact]
    [Trait("Scope", "Rules")]
    public void IsPaid_VietnamesePaidStatus()
    {
        Assert.Equal(true,InvoiceAccrual.IsPaid("Đã thanh toán"));
    }

    // UTC-INV-025
    [Fact]
    [Trait("Scope", "Rules")]
    public void IsPaid_UnpaidStatus()
    {
        Assert.Equal(false,InvoiceAccrual.IsPaid("UNPAID"));
    }

    // UTC-INV-026
    [Fact]
    [Trait("Scope", "Rules")]
    public void IsPaid_CancelledStatus()
    {
        Assert.Equal(false,InvoiceAccrual.IsPaid("CANCELLED"));
    }
}

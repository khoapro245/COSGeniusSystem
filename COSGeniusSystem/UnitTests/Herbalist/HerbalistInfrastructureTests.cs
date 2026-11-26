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

using static COSGeniusSystem.UnitTests.HerbalistTestSupport;

namespace COSGeniusSystem.UnitTests;

[Trait("Module", "Herbalist Infrastructure")]
public class HerbalistInfrastructureTests
{
    // UTC-HER-001
    [Fact]
    [Trait("Scope", "Controller")]
    public async Task Capabilities_ServiceSuccess_Returns200()
    {
        await CheckControllerAsync("Capabilities", RequisitionOutcome.Success, 200);
    }


    // UTC-HER-002
    [Fact]
    [Trait("Scope", "Controller")]
    public async Task Capabilities_ServiceNotFound_Returns404()
    {
        await CheckControllerAsync("Capabilities", RequisitionOutcome.NotFound, 404);
    }


    // UTC-HER-003
    [Fact]
    [Trait("Scope", "Controller")]
    public async Task Capabilities_ServiceSchemaUnavailable_Returns503()
    {
        await CheckControllerAsync("Capabilities", RequisitionOutcome.SchemaUnavailable, 503);
    }


    // UTC-HER-004
    [Fact]
    [Trait("Scope", "Controller")]
    public async Task Lookups_ServiceSuccess_Returns200()
    {
        await CheckControllerAsync("Lookups", RequisitionOutcome.Success, 200);
    }


    // UTC-HER-005
    [Fact]
    [Trait("Scope", "Controller")]
    public async Task Lookups_ServiceNotFound_Returns404()
    {
        await CheckControllerAsync("Lookups", RequisitionOutcome.NotFound, 404);
    }


    // UTC-HER-006
    [Fact]
    [Trait("Scope", "Controller")]
    public async Task Lookups_ServiceSchemaUnavailable_Returns503()
    {
        await CheckControllerAsync("Lookups", RequisitionOutcome.SchemaUnavailable, 503);
    }


    // UTC-HER-092
    [Fact]
    [Trait("Scope", "ExceptionFilter")]
    public void ConflictFilter_ConcurrencyExceptionReturns409()
    {
        var context = ErrorContext(new DbUpdateConcurrencyException("Changed"));
        new HerbalistConflictAttribute().OnException(context);
        Assert.True(context.ExceptionHandled);
        Assert.Equal(409, Assert.IsType<ObjectResult>(context.Result).StatusCode);
    }


    // UTC-HER-093
    [Fact]
    [Trait("Scope", "ExceptionFilter")]
    public void ConflictFilter_UnexpectedExceptionIsNotHandled()
    {
        var context = ErrorContext(new InvalidOperationException("Unexpected"));
        new HerbalistConflictAttribute().OnException(context);
        Assert.False(context.ExceptionHandled); Assert.Null(context.Result);
    }


    // UTC-HER-094
    [Fact]
    [Trait("Scope", "ExceptionFilter")]
    public void ConflictFilter_UnrelatedDatabaseExceptionIsNotHandled()
    {
        var context = ErrorContext(new DbUpdateException("Other database error", new ArgumentException("Invalid")));
        new HerbalistConflictAttribute().OnException(context);
        Assert.False(context.ExceptionHandled); Assert.Null(context.Result);
    }


    // UTC-HER-095
    [Fact]
    [Trait("Scope", "EfMetadata")]
    public void EfModel_CompositePrescriptionLinkAndFilteredUniqueIndex()
    {
        using var db = ModelDb();
        var line = db.Model.FindEntityType(typeof(HerbRequisitionDetail))!;
        Assert.Contains(line.GetForeignKeys(), fk => fk.PrincipalEntityType.ClrType == typeof(HerbRequisitionPrescription)
            && fk.Properties.Select(p => p.Name).SequenceEqual(new[] { "RequestId", "PrescriptionId" }));
        var link = db.Model.FindEntityType(typeof(HerbRequisitionPrescription))!;
        var unique = Assert.Single(link.GetIndexes().Where(i => i.IsUnique
            && i.Properties.Select(p => p.Name).SequenceEqual(new[] { "PrescriptionId" })));
        Assert.Equal("[IsActive] = 1", unique.GetFilter());
    }


    // UTC-HER-096
    [Fact]
    [Trait("Scope", "EfMetadata")]
    public void EfModel_StockAndDispensePrecision()
    {
        using var db = ModelDb();
        var stock = db.Model.FindEntityType(typeof(Batch))!.FindProperty("CurrentQuantity")!;
        Assert.Equal(18, stock.GetPrecision()); Assert.Equal(6, stock.GetScale());
        var allocated = db.Model.FindEntityType(typeof(DispenseDetail))!.FindProperty("QuantityTaken")!;
        Assert.Equal(18, allocated.GetPrecision()); Assert.Equal(3, allocated.GetScale());
    }


    // UTC-HER-097
    [Fact]
    [Trait("Scope", "EfMetadata")]
    public void EfModel_RowVersionAndComputedDifference()
    {
        using var db = ModelDb();
        var version = db.Model.FindEntityType(typeof(HerbRequisition))!.FindProperty("RowVersion")!;
        Assert.True(version.IsConcurrencyToken); Assert.Equal(ValueGenerated.OnAddOrUpdate, version.ValueGenerated);
        var difference = db.Model.FindEntityType(typeof(HerbReceiptDiscrepancyDetail))!.FindProperty("DifferenceQuantity")!;
        Assert.Equal("[ReceivedQuantity]-[ExpectedQuantity]", difference.GetComputedColumnSql());
        Assert.Equal(14, difference.GetPrecision()); Assert.Equal(3, difference.GetScale());
    }

}

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

[Trait("Module", "Herb Receipt Verification")]
public class HerbReceiptVerificationTests
{
    // UTC-HER-025
    [Fact]
    [Trait("Scope", "Controller")]
    public async Task Discrepancies_ServiceSuccess_Returns200()
    {
        await CheckControllerAsync("Discrepancies", RequisitionOutcome.Success, 200);
    }


    // UTC-HER-026
    [Fact]
    [Trait("Scope", "Controller")]
    public async Task Discrepancies_ServiceNotFound_Returns404()
    {
        await CheckControllerAsync("Discrepancies", RequisitionOutcome.NotFound, 404);
    }


    // UTC-HER-027
    [Fact]
    [Trait("Scope", "Controller")]
    public async Task Discrepancies_ServiceSchemaUnavailable_Returns503()
    {
        await CheckControllerAsync("Discrepancies", RequisitionOutcome.SchemaUnavailable, 503);
    }


    // UTC-HER-043
    [Fact]
    [Trait("Scope", "Controller")]
    public async Task Discrepancy_ServiceSuccess_Returns200()
    {
        await CheckControllerAsync("Discrepancy", RequisitionOutcome.Success, 200);
    }


    // UTC-HER-044
    [Fact]
    [Trait("Scope", "Controller")]
    public async Task Discrepancy_ServiceNotFound_Returns404()
    {
        await CheckControllerAsync("Discrepancy", RequisitionOutcome.NotFound, 404);
    }


    // UTC-HER-045
    [Fact]
    [Trait("Scope", "Controller")]
    public async Task Discrepancy_ServiceSchemaUnavailable_Returns503()
    {
        await CheckControllerAsync("Discrepancy", RequisitionOutcome.SchemaUnavailable, 503);
    }


    // UTC-HER-055
    [Fact]
    [Trait("Scope", "Controller")]
    public async Task Receive_ServiceSuccess_Returns200()
    {
        await CheckControllerAsync("Receive", RequisitionOutcome.Success, 200);
    }


    // UTC-HER-056
    [Fact]
    [Trait("Scope", "Controller")]
    public async Task Receive_ServiceInvalid_Returns400()
    {
        await CheckControllerAsync("Receive", RequisitionOutcome.Invalid, 400);
    }


    // UTC-HER-057
    [Fact]
    [Trait("Scope", "Controller")]
    public async Task Receive_ServiceConflict_Returns409()
    {
        await CheckControllerAsync("Receive", RequisitionOutcome.Conflict, 409);
    }


    // UTC-HER-064
    [Fact]
    [Trait("Scope", "Controller")]
    public async Task CreateDiscrepancy_ServiceSuccess_Returns200()
    {
        await CheckControllerAsync("CreateDiscrepancy", RequisitionOutcome.Success, 200);
    }


    // UTC-HER-065
    [Fact]
    [Trait("Scope", "Controller")]
    public async Task CreateDiscrepancy_ServiceInvalid_Returns400()
    {
        await CheckControllerAsync("CreateDiscrepancy", RequisitionOutcome.Invalid, 400);
    }


    // UTC-HER-066
    [Fact]
    [Trait("Scope", "Controller")]
    public async Task CreateDiscrepancy_ServiceConflict_Returns409()
    {
        await CheckControllerAsync("CreateDiscrepancy", RequisitionOutcome.Conflict, 409);
    }


}

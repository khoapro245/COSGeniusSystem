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

[Trait("Module", "Herb Requisitions")]
public class HerbRequisitionTests
{
    // UTC-HER-013
    [Fact]
    [Trait("Scope", "Controller")]
    public async Task Requests_ServiceSuccess_Returns200()
    {
        await CheckControllerAsync("Requests", RequisitionOutcome.Success, 200);
    }


    // UTC-HER-014
    [Fact]
    [Trait("Scope", "Controller")]
    public async Task Requests_ServiceNotFound_Returns404()
    {
        await CheckControllerAsync("Requests", RequisitionOutcome.NotFound, 404);
    }


    // UTC-HER-015
    [Fact]
    [Trait("Scope", "Controller")]
    public async Task Requests_ServiceSchemaUnavailable_Returns503()
    {
        await CheckControllerAsync("Requests", RequisitionOutcome.SchemaUnavailable, 503);
    }


    // UTC-HER-031
    [Fact]
    [Trait("Scope", "Controller")]
    public async Task RequestDetail_ServiceSuccess_Returns200()
    {
        await CheckControllerAsync("RequestDetail", RequisitionOutcome.Success, 200);
    }


    // UTC-HER-032
    [Fact]
    [Trait("Scope", "Controller")]
    public async Task RequestDetail_ServiceNotFound_Returns404()
    {
        await CheckControllerAsync("RequestDetail", RequisitionOutcome.NotFound, 404);
    }


    // UTC-HER-033
    [Fact]
    [Trait("Scope", "Controller")]
    public async Task RequestDetail_ServiceSchemaUnavailable_Returns503()
    {
        await CheckControllerAsync("RequestDetail", RequisitionOutcome.SchemaUnavailable, 503);
    }


    // UTC-HER-049
    [Fact]
    [Trait("Scope", "Controller")]
    public async Task Preview_ServiceSuccess_Returns200()
    {
        await CheckControllerAsync("Preview", RequisitionOutcome.Success, 200);
    }


    // UTC-HER-050
    [Fact]
    [Trait("Scope", "Controller")]
    public async Task Preview_ServiceUnprocessable_Returns422()
    {
        await CheckControllerAsync("Preview", RequisitionOutcome.Unprocessable, 422);
    }


    // UTC-HER-051
    [Fact]
    [Trait("Scope", "Controller")]
    public async Task Preview_ServiceSchemaUnavailable_Returns503()
    {
        await CheckControllerAsync("Preview", RequisitionOutcome.SchemaUnavailable, 503);
    }


    // UTC-HER-052
    [Fact]
    [Trait("Scope", "Controller")]
    public async Task Create_ServiceSuccess_Returns200()
    {
        await CheckControllerAsync("Create", RequisitionOutcome.Success, 200);
    }


    // UTC-HER-053
    [Fact]
    [Trait("Scope", "Controller")]
    public async Task Create_ServiceInvalid_Returns400()
    {
        await CheckControllerAsync("Create", RequisitionOutcome.Invalid, 400);
    }


    // UTC-HER-054
    [Fact]
    [Trait("Scope", "Controller")]
    public async Task Create_ServiceConflict_Returns409()
    {
        await CheckControllerAsync("Create", RequisitionOutcome.Conflict, 409);
    }


    // UTC-HER-061
    [Fact]
    [Trait("Scope", "Controller")]
    public async Task Cancel_ServiceSuccess_Returns200()
    {
        await CheckControllerAsync("Cancel", RequisitionOutcome.Success, 200);
    }


    // UTC-HER-062
    [Fact]
    [Trait("Scope", "Controller")]
    public async Task Cancel_ServiceInvalid_Returns400()
    {
        await CheckControllerAsync("Cancel", RequisitionOutcome.Invalid, 400);
    }


    // UTC-HER-063
    [Fact]
    [Trait("Scope", "Controller")]
    public async Task Cancel_ServiceConflict_Returns409()
    {
        await CheckControllerAsync("Cancel", RequisitionOutcome.Conflict, 409);
    }


}

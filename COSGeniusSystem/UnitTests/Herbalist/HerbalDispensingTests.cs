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

[Trait("Module", "Herbal Dispensing")]
public class HerbalDispensingTests
{
    // UTC-HER-058
    [Fact]
    [Trait("Scope", "Controller")]
    public async Task Complete_ServiceSuccess_Returns200()
    {
        await CheckControllerAsync("Complete", RequisitionOutcome.Success, 200);
    }


    // UTC-HER-059
    [Fact]
    [Trait("Scope", "Controller")]
    public async Task Complete_ServiceInvalid_Returns400()
    {
        await CheckControllerAsync("Complete", RequisitionOutcome.Invalid, 400);
    }


    // UTC-HER-060
    [Fact]
    [Trait("Scope", "Controller")]
    public async Task Complete_ServiceConflict_Returns409()
    {
        await CheckControllerAsync("Complete", RequisitionOutcome.Conflict, 409);
    }


    // UTC-HER-079
    [Fact]
    [Trait("Scope", "Rules")]
    public void UsableReceipt_UncheckedReceipt()
    {
        Assert.Equal(0m, HerbalistWorkflowRules.UsableReceipt(100m, null));
    }


    // UTC-HER-080
    [Fact]
    [Trait("Scope", "Rules")]
    public void UsableReceipt_ExactReceipt()
    {
        Assert.Equal(100m, HerbalistWorkflowRules.UsableReceipt(100m, 100m));
    }


    // UTC-HER-081
    [Fact]
    [Trait("Scope", "Rules")]
    public void UsableReceipt_ShortReceipt()
    {
        Assert.Equal(0m, HerbalistWorkflowRules.UsableReceipt(100m, 90m));
    }


    // UTC-HER-082
    [Fact]
    [Trait("Scope", "Rules")]
    public void UsableReceipt_SurplusReceipt()
    {
        Assert.Equal(0m, HerbalistWorkflowRules.UsableReceipt(100m, 110m));
    }


    // UTC-HER-083
    [Fact]
    [Trait("Scope", "Rules")]
    public void FitsReceipt_ExactRemainingBudget()
    {
        Assert.Equal(true, HerbalistWorkflowRules.FitsReceipt(100m, 100m, 20m, 80m));
    }


    // UTC-HER-084
    [Fact]
    [Trait("Scope", "Rules")]
    public void FitsReceipt_ExceedsRemainingBudget()
    {
        Assert.Equal(false, HerbalistWorkflowRules.FitsReceipt(100m, 100m, 20m, 81m));
    }


    // UTC-HER-085
    [Fact]
    [Trait("Scope", "Rules")]
    public void FitsReceipt_DiscrepancyCannotBeAllocated()
    {
        Assert.Equal(false, HerbalistWorkflowRules.FitsReceipt(100m, 90m, 0m, 5m));
    }


    // UTC-HER-086
    [Fact]
    [Trait("Scope", "Rules")]
    public void FitsReceipt_ZeroAllocation()
    {
        Assert.Equal(false, HerbalistWorkflowRules.FitsReceipt(100m, 100m, 0m, 0m));
    }


    // UTC-HER-087
    [Fact]
    [Trait("Scope", "Rules")]
    public void FitsReceipt_NegativePreviousUsage()
    {
        Assert.Equal(false, HerbalistWorkflowRules.FitsReceipt(100m, 100m, -1m, 5m));
    }


}

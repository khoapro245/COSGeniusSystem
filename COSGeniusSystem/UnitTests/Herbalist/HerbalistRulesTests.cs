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

[Trait("Module", "Herbalist Stock Rules")]
public class HerbalistRulesTests
{
    // UTC-HER-067
    [Fact]
    [Trait("Scope", "Rules")]
    public void SupportsStockUnit_Kilograms()
    {
        Assert.Equal(true, HerbalistWorkflowRules.SupportsStockUnit("kg"));
    }


    // UTC-HER-068
    [Fact]
    [Trait("Scope", "Rules")]
    public void SupportsStockUnit_Grams()
    {
        Assert.Equal(true, HerbalistWorkflowRules.SupportsStockUnit("g"));
    }


    // UTC-HER-069
    [Fact]
    [Trait("Scope", "Rules")]
    public void SupportsStockUnit_UnsupportedUnit()
    {
        Assert.Equal(false, HerbalistWorkflowRules.SupportsStockUnit("ml"));
    }


    // UTC-HER-070
    [Fact]
    [Trait("Scope", "Rules")]
    public void SupportsStockUnit_NullUnit()
    {
        Assert.Equal(false, HerbalistWorkflowRules.SupportsStockUnit(null));
    }


    // UTC-HER-071
    [Fact]
    [Trait("Scope", "Rules")]
    public void StockToGrams_KilogramsToGrams()
    {
        Assert.Equal(1234m, HerbalistWorkflowRules.StockToGrams(1.234m, "kg"));
    }


    // UTC-HER-072
    [Fact]
    [Trait("Scope", "Rules")]
    public void StockToGrams_GramsUnchanged()
    {
        Assert.Equal(12.345m, HerbalistWorkflowRules.StockToGrams(12.345m, "g"));
    }


    // UTC-HER-073
    [Fact]
    [Trait("Scope", "Rules")]
    public void StockToGrams_ZeroQuantity()
    {
        Assert.Equal(0m, HerbalistWorkflowRules.StockToGrams(0m, "kg"));
    }


    // UTC-HER-074
    [Fact]
    [Trait("Scope", "Rules")]
    public void StockToGrams_UnsupportedUnitThrowsArgumentException()
    {
        Assert.Throws<ArgumentException>(() => HerbalistWorkflowRules.StockToGrams(1m, "ml"));
    }


    // UTC-HER-075
    [Fact]
    [Trait("Scope", "Rules")]
    public void GramsToStock_GramsToKilograms()
    {
        Assert.Equal(1m, HerbalistWorkflowRules.GramsToStock(1000m, "kg"));
    }


    // UTC-HER-076
    [Fact]
    [Trait("Scope", "Rules")]
    public void GramsToStock_GramsUnchanged()
    {
        Assert.Equal(12.345m, HerbalistWorkflowRules.GramsToStock(12.345m, "g"));
    }


    // UTC-HER-077
    [Fact]
    [Trait("Scope", "Rules")]
    public void GramsToStock_ZeroQuantity()
    {
        Assert.Equal(0m, HerbalistWorkflowRules.GramsToStock(0m, "g"));
    }


    // UTC-HER-078
    [Fact]
    [Trait("Scope", "Rules")]
    public void GramsToStock_UnsupportedUnitThrowsArgumentException()
    {
        Assert.Throws<ArgumentException>(() => HerbalistWorkflowRules.GramsToStock(1m, "ml"));
    }


    // UTC-HER-088
    [Fact]
    [Trait("Scope", "Rules")]
    public void FefoAvailableGrams_KilogramsConverted()
    {
        Assert.Equal(1.234m, HerbalistWorkflowRules.FefoAvailableGrams(0.001234m, "kg"));
    }


    // UTC-HER-089
    [Fact]
    [Trait("Scope", "Rules")]
    public void FefoAvailableGrams_FractionFloored()
    {
        Assert.Equal(12.345m, HerbalistWorkflowRules.FefoAvailableGrams(12.3459m, "g"));
    }


    // UTC-HER-090
    [Fact]
    [Trait("Scope", "Rules")]
    public void FefoAvailableGrams_NegativeStockClamped()
    {
        Assert.Equal(0m, HerbalistWorkflowRules.FefoAvailableGrams(-1m, "g"));
    }


    // UTC-HER-091
    [Fact]
    [Trait("Scope", "Rules")]
    public void FefoAvailableGrams_UnsupportedUnitThrowsArgumentException()
    {
        Assert.Throws<ArgumentException>(() => HerbalistWorkflowRules.FefoAvailableGrams(1m, "ml"));
    }


}

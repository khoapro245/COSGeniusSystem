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

[Trait("Module", "Dispensing Prescriptions")]
public class DispensingPrescriptionTests
{
    // UTC-HER-007
    [Fact]
    [Trait("Scope", "Controller")]
    public async Task ListPrescriptions_ServiceSuccess_Returns200()
    {
        await CheckControllerAsync("ListPrescriptions", RequisitionOutcome.Success, 200);
    }


    // UTC-HER-008
    [Fact]
    [Trait("Scope", "Controller")]
    public async Task ListPrescriptions_ServiceNotFound_Returns404()
    {
        await CheckControllerAsync("ListPrescriptions", RequisitionOutcome.NotFound, 404);
    }


    // UTC-HER-009
    [Fact]
    [Trait("Scope", "Controller")]
    public async Task ListPrescriptions_ServiceSchemaUnavailable_Returns503()
    {
        await CheckControllerAsync("ListPrescriptions", RequisitionOutcome.SchemaUnavailable, 503);
    }


    // UTC-HER-010
    [Fact]
    [Trait("Scope", "Controller")]
    public async Task History_ServiceSuccess_Returns200()
    {
        await CheckControllerAsync("History", RequisitionOutcome.Success, 200);
    }


    // UTC-HER-011
    [Fact]
    [Trait("Scope", "Controller")]
    public async Task History_ServiceNotFound_Returns404()
    {
        await CheckControllerAsync("History", RequisitionOutcome.NotFound, 404);
    }


    // UTC-HER-012
    [Fact]
    [Trait("Scope", "Controller")]
    public async Task History_ServiceSchemaUnavailable_Returns503()
    {
        await CheckControllerAsync("History", RequisitionOutcome.SchemaUnavailable, 503);
    }


    // UTC-HER-016
    [Fact]
    [Trait("Scope", "Controller")]
    public async Task Patients_ServiceSuccess_Returns200()
    {
        await CheckControllerAsync("Patients", RequisitionOutcome.Success, 200);
    }


    // UTC-HER-017
    [Fact]
    [Trait("Scope", "Controller")]
    public async Task Patients_ServiceNotFound_Returns404()
    {
        await CheckControllerAsync("Patients", RequisitionOutcome.NotFound, 404);
    }


    // UTC-HER-018
    [Fact]
    [Trait("Scope", "Controller")]
    public async Task Patients_ServiceSchemaUnavailable_Returns503()
    {
        await CheckControllerAsync("Patients", RequisitionOutcome.SchemaUnavailable, 503);
    }


    // UTC-HER-019
    [Fact]
    [Trait("Scope", "Controller")]
    public async Task Herbs_ServiceSuccess_Returns200()
    {
        await CheckControllerAsync("Herbs", RequisitionOutcome.Success, 200);
    }


    // UTC-HER-020
    [Fact]
    [Trait("Scope", "Controller")]
    public async Task Herbs_ServiceNotFound_Returns404()
    {
        await CheckControllerAsync("Herbs", RequisitionOutcome.NotFound, 404);
    }


    // UTC-HER-021
    [Fact]
    [Trait("Scope", "Controller")]
    public async Task Herbs_ServiceSchemaUnavailable_Returns503()
    {
        await CheckControllerAsync("Herbs", RequisitionOutcome.SchemaUnavailable, 503);
    }


    // UTC-HER-022
    [Fact]
    [Trait("Scope", "Controller")]
    public async Task Formulas_ServiceSuccess_Returns200()
    {
        await CheckControllerAsync("Formulas", RequisitionOutcome.Success, 200);
    }


    // UTC-HER-023
    [Fact]
    [Trait("Scope", "Controller")]
    public async Task Formulas_ServiceNotFound_Returns404()
    {
        await CheckControllerAsync("Formulas", RequisitionOutcome.NotFound, 404);
    }


    // UTC-HER-024
    [Fact]
    [Trait("Scope", "Controller")]
    public async Task Formulas_ServiceSchemaUnavailable_Returns503()
    {
        await CheckControllerAsync("Formulas", RequisitionOutcome.SchemaUnavailable, 503);
    }


    // UTC-HER-028
    [Fact]
    [Trait("Scope", "Controller")]
    public async Task Prescription_ServiceSuccess_Returns200()
    {
        await CheckControllerAsync("Prescription", RequisitionOutcome.Success, 200);
    }


    // UTC-HER-029
    [Fact]
    [Trait("Scope", "Controller")]
    public async Task Prescription_ServiceNotFound_Returns404()
    {
        await CheckControllerAsync("Prescription", RequisitionOutcome.NotFound, 404);
    }


    // UTC-HER-030
    [Fact]
    [Trait("Scope", "Controller")]
    public async Task Prescription_ServiceSchemaUnavailable_Returns503()
    {
        await CheckControllerAsync("Prescription", RequisitionOutcome.SchemaUnavailable, 503);
    }


    // UTC-HER-034
    [Fact]
    [Trait("Scope", "Controller")]
    public async Task PatientDetail_ServiceSuccess_Returns200()
    {
        await CheckControllerAsync("PatientDetail", RequisitionOutcome.Success, 200);
    }


    // UTC-HER-035
    [Fact]
    [Trait("Scope", "Controller")]
    public async Task PatientDetail_ServiceNotFound_Returns404()
    {
        await CheckControllerAsync("PatientDetail", RequisitionOutcome.NotFound, 404);
    }


    // UTC-HER-036
    [Fact]
    [Trait("Scope", "Controller")]
    public async Task PatientDetail_ServiceSchemaUnavailable_Returns503()
    {
        await CheckControllerAsync("PatientDetail", RequisitionOutcome.SchemaUnavailable, 503);
    }


    // UTC-HER-037
    [Fact]
    [Trait("Scope", "Controller")]
    public async Task HerbDetail_ServiceSuccess_Returns200()
    {
        await CheckControllerAsync("HerbDetail", RequisitionOutcome.Success, 200);
    }


    // UTC-HER-038
    [Fact]
    [Trait("Scope", "Controller")]
    public async Task HerbDetail_ServiceNotFound_Returns404()
    {
        await CheckControllerAsync("HerbDetail", RequisitionOutcome.NotFound, 404);
    }


    // UTC-HER-039
    [Fact]
    [Trait("Scope", "Controller")]
    public async Task HerbDetail_ServiceSchemaUnavailable_Returns503()
    {
        await CheckControllerAsync("HerbDetail", RequisitionOutcome.SchemaUnavailable, 503);
    }


    // UTC-HER-040
    [Fact]
    [Trait("Scope", "Controller")]
    public async Task FormulaDetail_ServiceSuccess_Returns200()
    {
        await CheckControllerAsync("FormulaDetail", RequisitionOutcome.Success, 200);
    }


    // UTC-HER-041
    [Fact]
    [Trait("Scope", "Controller")]
    public async Task FormulaDetail_ServiceNotFound_Returns404()
    {
        await CheckControllerAsync("FormulaDetail", RequisitionOutcome.NotFound, 404);
    }


    // UTC-HER-042
    [Fact]
    [Trait("Scope", "Controller")]
    public async Task FormulaDetail_ServiceSchemaUnavailable_Returns503()
    {
        await CheckControllerAsync("FormulaDetail", RequisitionOutcome.SchemaUnavailable, 503);
    }


    // UTC-HER-046
    [Fact]
    [Trait("Scope", "Controller")]
    public async Task PaymentEligibility_ServiceSuccess_Returns200()
    {
        await CheckControllerAsync("PaymentEligibility", RequisitionOutcome.Success, 200);
    }


    // UTC-HER-047
    [Fact]
    [Trait("Scope", "Controller")]
    public async Task PaymentEligibility_ServiceNotFound_Returns404()
    {
        await CheckControllerAsync("PaymentEligibility", RequisitionOutcome.NotFound, 404);
    }


    // UTC-HER-048
    [Fact]
    [Trait("Scope", "Controller")]
    public async Task PaymentEligibility_ServiceSchemaUnavailable_Returns503()
    {
        await CheckControllerAsync("PaymentEligibility", RequisitionOutcome.SchemaUnavailable, 503);
    }


}

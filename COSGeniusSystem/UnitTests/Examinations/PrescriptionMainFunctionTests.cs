using System;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using COSGeniusSystem.Commons;
using COSGeniusSystem.Data;
using COSGeniusSystem.DTOs;
using COSGeniusSystem.Exceptions;
using COSGeniusSystem.Models;
using COSGeniusSystem.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Xunit;

namespace COSGeniusSystem.UnitTests;

public sealed class PrescriptionMainFunctionTests : ClinicalOrderTestSupport
{
    #region ExaminationService.CreatePrescriptionAsync

    // UTC-RX-MAIN-001: Tested method = ExaminationService.CreatePrescriptionAsync
    [Fact]
    [Trait("Module", "RX")]
    [Trait("Method", "CreatePrescriptionAsync")]
    [Trait("TestCaseId", "UTC-RX-MAIN-001")]
    public async Task CreatePrescriptionAsync_EditableExamination_CreatesDraft()
    {
        using var s = await Scope.CreateAsync();
        var result = await s.Api.CreatePrescriptionAsync(DoctorActor(), 1, default);
        Assert.Equal("DRAFT", result.Status); Assert.Equal(1, result.TotalDoses);
        Assert.Equal(s.Record.RecordId, result.RecordId); Assert.Empty(result.Items);
        s.Db.ChangeTracker.Clear();
        Assert.Equal(result.PrescriptionId, (await s.Db.HerbalPrescriptions.SingleAsync()).PrescriptionId);
        Assert.False(await s.Db.Invoices.AnyAsync());
    }

    // UTC-RX-MAIN-002: Tested method = ExaminationService.CreatePrescriptionAsync
    [Fact]
    [Trait("Module", "RX")]
    [Trait("Method", "CreatePrescriptionAsync")]
    [Trait("TestCaseId", "UTC-RX-MAIN-002")]
    public async Task CreatePrescriptionAsync_CompletedExamination_ThrowsConflict()
    {
        using var s = await Scope.CreateAsync(); await s.CompletedAsync();
        var before = await s.OrdersSnapshotAsync();
        await Assert.ThrowsAsync<ConflictException>(() => s.Api.CreatePrescriptionAsync(DoctorActor(), 1, default));
        Assert.Equal(before, await s.OrdersSnapshotAsync());
        Assert.False(await s.Db.HerbalPrescriptions.AnyAsync());
    }

    // UTC-RX-MAIN-003: Tested method = ExaminationService.CreatePrescriptionAsync
    [Fact]
    [Trait("Module", "RX")]
    [Trait("Method", "CreatePrescriptionAsync")]
    [Trait("TestCaseId", "UTC-RX-MAIN-003")]
    public async Task CreatePrescriptionAsync_UnauthorizedDoctor_ThrowsForbidden()
    {
        using var s = await Scope.CreateAsync();
        var before = await s.OrdersSnapshotAsync();
        await Assert.ThrowsAsync<ForbiddenException>(() => s.Api.CreatePrescriptionAsync(DoctorActor(99), 1, default));
        Assert.Equal(before, await s.OrdersSnapshotAsync());
    }

    #endregion

    #region ExaminationService.SavePrescriptionAsync

    // UTC-RX-MAIN-004: Tested method = ExaminationService.SavePrescriptionAsync
    [Fact]
    [Trait("Module", "RX")]
    [Trait("Method", "SavePrescriptionAsync")]
    [Trait("TestCaseId", "UTC-RX-MAIN-004")]
    public async Task SavePrescriptionAsync_ValidDraft_SavesQuantityAndInstructions()
    {
        using var s = await Scope.CreateAsync(); var rx = await s.PrescriptionAsync();
        var originalId = rx.HerbalPrescriptionDetails.Single().PrescriptionDetailId;
        var result = await s.Api.SavePrescriptionAsync(DoctorActor(), 1, rx.PrescriptionId, Scope.Medicine(rx), default);
        Assert.Equal(3, result.TotalDoses); Assert.Equal("Edited instructions", result.UsageInstructions);
        var item = Assert.Single(result.Items);
        Assert.Equal(12m, item.DosagePerDose); Assert.Equal(36m, item.TotalQuantity);
        Assert.Equal(originalId, item.PrescriptionDetailId);
        s.Db.ChangeTracker.Clear();
        Assert.Equal(3, (await s.Db.HerbalPrescriptions.SingleAsync()).TotalDoses);
        Assert.Equal(100m, (await s.Db.Batches.SingleAsync()).CurrentQuantity);
    }

    // UTC-RX-MAIN-005: Tested method = ExaminationService.SavePrescriptionAsync
    [Fact]
    [Trait("Module", "RX")]
    [Trait("Method", "SavePrescriptionAsync")]
    [Trait("TestCaseId", "UTC-RX-MAIN-005")]
    public async Task SavePrescriptionAsync_SentPrescription_RemainsEditableUntilCompletion()
    {
        using var s = await Scope.CreateAsync(); var rx = await s.PrescriptionAsync();
        rx.Status = "SENT"; rx.SentAt = DateTime.UtcNow; await s.Db.SaveChangesAsync();
        var result = await s.Api.SavePrescriptionAsync(DoctorActor(), 1, rx.PrescriptionId, Scope.Medicine(rx), default);
        Assert.Equal(3, result.TotalDoses); Assert.Equal("SENT", result.Status);
        await s.CompletedAsync();
        var before = await s.OrdersSnapshotAsync();
        await Assert.ThrowsAsync<ConflictException>(() => s.Api.SavePrescriptionAsync(DoctorActor(), 1,
            rx.PrescriptionId, Scope.Medicine(rx), default));
        Assert.Equal(before, await s.OrdersSnapshotAsync());
    }

    // UTC-RX-MAIN-006: Tested method = ExaminationService.SavePrescriptionAsync
    [Fact]
    [Trait("Module", "RX")]
    [Trait("Method", "SavePrescriptionAsync")]
    [Trait("TestCaseId", "UTC-RX-MAIN-006")]
    public async Task SavePrescriptionAsync_DuplicateHerbs_ThrowsBadRequest()
    {
        using var s = await Scope.CreateAsync(); var rx = await s.PrescriptionAsync();
        var request = Scope.Medicine(rx); request.Items.Add(new() { HerbId = 1, DosagePerDose = 5m });
        var before = await s.OrdersSnapshotAsync();
        await Assert.ThrowsAsync<BadRequestException>(() => s.Api.SavePrescriptionAsync(DoctorActor(), 1, 1, request, default));
        Assert.Equal(before, await s.OrdersSnapshotAsync());
        s.Db.ChangeTracker.Clear();
        Assert.Equal(2, (await s.Db.HerbalPrescriptions.SingleAsync()).TotalDoses);
    }

    // UTC-RX-MAIN-007: Tested method = ExaminationService.SavePrescriptionAsync
    [Fact]
    [Trait("Module", "RX")]
    [Trait("Method", "SavePrescriptionAsync")]
    [Trait("TestCaseId", "UTC-RX-MAIN-007")]
    public async Task SavePrescriptionAsync_InactiveHerb_ThrowsBadRequest()
    {
        using var s = await Scope.CreateAsync(); var rx = await s.PrescriptionAsync();
        s.Herb().IsActive = false; await s.Db.SaveChangesAsync();
        var before = await s.OrdersSnapshotAsync();
        await Assert.ThrowsAsync<BadRequestException>(() => s.Api.SavePrescriptionAsync(DoctorActor(), 1, 1, Scope.Medicine(rx), default));
        Assert.Equal(before, await s.OrdersSnapshotAsync());
    }

    // UTC-RX-MAIN-008: Tested method = ExaminationService.SavePrescriptionAsync
    [Fact]
    [Trait("Module", "RX")]
    [Trait("Method", "SavePrescriptionAsync")]
    [Trait("TestCaseId", "UTC-RX-MAIN-008")]
    public async Task SavePrescriptionAsync_StaleVersion_ThrowsConflict()
    {
        using var s = await Scope.CreateAsync(); var rx = await s.PrescriptionAsync();
        var request = Scope.Medicine(rx); request.RowVersion = Stale(rx.RowVersion);
        var before = await s.OrdersSnapshotAsync();
        await Assert.ThrowsAsync<ConflictException>(() => s.Api.SavePrescriptionAsync(DoctorActor(), 1, 1, request, default));
        Assert.Equal(before, await s.OrdersSnapshotAsync());
    }

    #endregion

    #region ExaminationService.LoadFormulaAsync

    // UTC-RX-MAIN-009: Tested method = ExaminationService.LoadFormulaAsync
    [Fact]
    [Trait("Module", "RX")]
    [Trait("Method", "LoadFormulaAsync")]
    [Trait("TestCaseId", "UTC-RX-MAIN-009")]
    public async Task LoadFormulaAsync_ApprovedFormula_CopiesHerbsAndInstructions()
    {
        using var s = await Scope.CreateAsync(); var rx = await s.PrescriptionAsync(withItems: false);
        var formula = await s.FormulaAsync();
        var result = await s.Api.LoadFormulaAsync(DoctorActor(), 1, 1,
            new LoadFormulaRequest { RowVersion = rx.RowVersion, HerbalFormulaId = formula.HerbalFormulaId }, default);
        Assert.Equal(formula.HerbalFormulaId, result.SourceHerbalFormulaId);
        Assert.Equal(formula.FormulaName, result.PrescriptionName);
        Assert.Equal(formula.UsageInstructions, result.UsageInstructions);
        Assert.Equal(12m, Assert.Single(result.Items).DosagePerDose);
        Assert.Equal(2, result.TotalDoses);
        Assert.Equal(24m, Assert.Single(result.Items).TotalQuantity);
        s.Db.ChangeTracker.Clear();
        Assert.Equal(formula.HerbalFormulaId, (await s.Db.HerbalPrescriptions.SingleAsync()).SourceHerbalFormulaId);
        var savedFormula = await s.Db.HerbalFormulas.Include(f => f.HerbalFormulaDetails).SingleAsync();
        Assert.Equal(12m, Assert.Single(savedFormula.HerbalFormulaDetails).DosagePerDose);
        Assert.Equal(100m, (await s.Db.Batches.SingleAsync()).CurrentQuantity);
    }

    // UTC-RX-MAIN-010: Tested method = ExaminationService.LoadFormulaAsync
    [Fact]
    [Trait("Module", "RX")]
    [Trait("Method", "LoadFormulaAsync")]
    [Trait("TestCaseId", "UTC-RX-MAIN-010")]
    public async Task LoadFormulaAsync_UnapprovedFormula_ThrowsBadRequest()
    {
        using var s = await Scope.CreateAsync(); var rx = await s.PrescriptionAsync();
        var formula = await s.FormulaAsync(status: "DRAFT");
        var before = await s.OrdersSnapshotAsync();
        await Assert.ThrowsAsync<BadRequestException>(() => s.Api.LoadFormulaAsync(DoctorActor(), 1, 1,
            new LoadFormulaRequest { RowVersion = rx.RowVersion, HerbalFormulaId = formula.HerbalFormulaId }, default));
        Assert.Equal(before, await s.OrdersSnapshotAsync());
    }

    // UTC-RX-MAIN-011: Tested method = ExaminationService.LoadFormulaAsync
    [Fact]
    [Trait("Module", "RX")]
    [Trait("Method", "LoadFormulaAsync")]
    [Trait("TestCaseId", "UTC-RX-MAIN-011")]
    public async Task LoadFormulaAsync_InactiveFormulaHerb_ThrowsBadRequest()
    {
        using var s = await Scope.CreateAsync(); var rx = await s.PrescriptionAsync();
        var formula = await s.FormulaAsync(); s.Herb().IsActive = false; await s.Db.SaveChangesAsync();
        var before = await s.OrdersSnapshotAsync();
        await Assert.ThrowsAsync<BadRequestException>(() => s.Api.LoadFormulaAsync(DoctorActor(), 1, 1,
            new LoadFormulaRequest { RowVersion = rx.RowVersion, HerbalFormulaId = formula.HerbalFormulaId }, default));
        Assert.Equal(before, await s.OrdersSnapshotAsync());
    }

    // UTC-RX-MAIN-012: Tested method = ExaminationService.LoadFormulaAsync
    [Fact]
    [Trait("Module", "RX")]
    [Trait("Method", "LoadFormulaAsync")]
    [Trait("TestCaseId", "UTC-RX-MAIN-012")]
    public async Task LoadFormulaAsync_StaleVersion_ThrowsConflict()
    {
        using var s = await Scope.CreateAsync(); var rx = await s.PrescriptionAsync(); await s.FormulaAsync();
        var before = await s.OrdersSnapshotAsync();
        await Assert.ThrowsAsync<ConflictException>(() => s.Api.LoadFormulaAsync(DoctorActor(), 1, 1,
            new LoadFormulaRequest { RowVersion = Stale(rx.RowVersion), HerbalFormulaId = 1 }, default));
        Assert.Equal(before, await s.OrdersSnapshotAsync());
    }

    #endregion

    #region ExaminationService.SendPrescriptionAsync

    // UTC-RX-MAIN-013: Tested method = ExaminationService.SendPrescriptionAsync
    [Fact]
    [Trait("Module", "RX")]
    [Trait("Method", "SendPrescriptionAsync")]
    [Trait("TestCaseId", "UTC-RX-MAIN-013")]
    public async Task SendPrescriptionAsync_SufficientStock_MarksSent()
    {
        using var s = await Scope.CreateAsync(); var rx = await s.PrescriptionAsync();
        var result = await s.Api.SendPrescriptionAsync(DoctorActor(), 1, 1,
            new VersionRequest { RowVersion = rx.RowVersion }, default);
        Assert.Equal("SENT", result.Status); Assert.NotNull(result.SentAt);
        Assert.Equal(1000m, Assert.Single(result.Items).UnitPrice);
        s.Db.ChangeTracker.Clear();
        Assert.Equal("SENT", (await s.Db.HerbalPrescriptions.SingleAsync()).Status);
        Assert.Equal(100m, (await s.Db.Batches.SingleAsync()).CurrentQuantity);
        Assert.False(await s.Db.Invoices.AnyAsync());
    }

    // UTC-RX-MAIN-014: Tested method = ExaminationService.SendPrescriptionAsync
    [Fact]
    [Trait("Module", "RX")]
    [Trait("Method", "SendPrescriptionAsync")]
    [Trait("TestCaseId", "UTC-RX-MAIN-014")]
    public async Task SendPrescriptionAsync_InsufficientStock_ThrowsConflict_CurrentContract()
    {
        using var s = await Scope.CreateAsync(); var rx = await s.PrescriptionAsync(stock: 15m);
        var before = await s.OrdersSnapshotAsync();
        var error = await Assert.ThrowsAsync<ConflictException>(() => s.Api.SendPrescriptionAsync(DoctorActor(), 1, 1,
            new VersionRequest { RowVersion = rx.RowVersion }, default));
        Assert.Equal(before, await s.OrdersSnapshotAsync());
        Assert.Contains("Herb 1", error.Message); Assert.Contains("20", error.Message); Assert.Contains("15", error.Message);
        s.Db.ChangeTracker.Clear();
        Assert.Equal("DRAFT", (await s.Db.HerbalPrescriptions.SingleAsync()).Status);
        Assert.Equal(15m, (await s.Db.Batches.SingleAsync()).CurrentQuantity);
    }

    // UTC-RX-MAIN-015: Tested method = ExaminationService.SendPrescriptionAsync
    [Fact]
    [Trait("Module", "RX")]
    [Trait("Method", "SendPrescriptionAsync")]
    [Trait("TestCaseId", "UTC-RX-MAIN-015")]
    public async Task SendPrescriptionAsync_MissingUsageInstructions_ThrowsBadRequest()
    {
        using var s = await Scope.CreateAsync(); var rx = await s.PrescriptionAsync();
        rx.UsageInstructions = ""; await s.Db.SaveChangesAsync();
        var before = await s.OrdersSnapshotAsync();
        await Assert.ThrowsAsync<BadRequestException>(() => s.Api.SendPrescriptionAsync(DoctorActor(), 1, 1,
            new VersionRequest { RowVersion = rx.RowVersion }, default));
        Assert.Equal(before, await s.OrdersSnapshotAsync());
    }

    // UTC-RX-MAIN-016: Tested method = ExaminationService.SendPrescriptionAsync
    [Fact]
    [Trait("Module", "RX")]
    [Trait("Method", "SendPrescriptionAsync")]
    [Trait("TestCaseId", "UTC-RX-MAIN-016")]
    public async Task SendPrescriptionAsync_AlreadySent_IsIdempotent()
    {
        using var s = await Scope.CreateAsync(); var rx = await s.PrescriptionAsync();
        var request = new VersionRequest { RowVersion = rx.RowVersion };
        var first = await s.Api.SendPrescriptionAsync(DoctorActor(), 1, 1, request, default);
        var second = await s.Api.SendPrescriptionAsync(DoctorActor(), 1, 1, request, default);
        Assert.Equal(first.SentAt, second.SentAt); Assert.Equal(first.PrescriptionId, second.PrescriptionId);
        Assert.Equal(1, await s.Db.HerbalPrescriptions.CountAsync());
    }

    // UTC-RX-MAIN-017: Tested method = ExaminationService.SendPrescriptionAsync
    [Fact]
    [Trait("Module", "RX")]
    [Trait("Method", "SendPrescriptionAsync")]
    [Trait("TestCaseId", "UTC-RX-MAIN-017")]
    public async Task SendPrescriptionAsync_StaleVersion_ThrowsConflict()
    {
        using var s = await Scope.CreateAsync(); var rx = await s.PrescriptionAsync();
        var before = await s.OrdersSnapshotAsync();
        await Assert.ThrowsAsync<ConflictException>(() => s.Api.SendPrescriptionAsync(DoctorActor(), 1, 1,
            new VersionRequest { RowVersion = Stale(rx.RowVersion) }, default));
        Assert.Equal(before, await s.OrdersSnapshotAsync());
    }

    #endregion

    #region ExaminationService.DeletePrescriptionAsync

    // UTC-RX-MAIN-018: Tested method = ExaminationService.DeletePrescriptionAsync
    [Fact]
    [Trait("Module", "RX")]
    [Trait("Method", "DeletePrescriptionAsync")]
    [Trait("TestCaseId", "UTC-RX-MAIN-018")]
    public async Task DeletePrescriptionAsync_Draft_RemovesPrescriptionAndDetails()
    {
        using var s = await Scope.CreateAsync(); var rx = await s.PrescriptionAsync();
        await s.Api.DeletePrescriptionAsync(DoctorActor(), 1, 1, new VersionRequest { RowVersion = rx.RowVersion }, default);
        s.Db.ChangeTracker.Clear();
        Assert.False(await s.Db.HerbalPrescriptions.AnyAsync());
        Assert.False(await s.Db.HerbalPrescriptionDetails.AnyAsync());
        Assert.Equal(100m, (await s.Db.Batches.SingleAsync()).CurrentQuantity);
    }

    // UTC-RX-MAIN-019: Tested method = ExaminationService.DeletePrescriptionAsync
    [Fact]
    [Trait("Module", "RX")]
    [Trait("Method", "DeletePrescriptionAsync")]
    [Trait("TestCaseId", "UTC-RX-MAIN-019")]
    public async Task DeletePrescriptionAsync_SentPrescription_ThrowsConflict()
    {
        using var s = await Scope.CreateAsync(); var rx = await s.PrescriptionAsync();
        rx.Status = "SENT"; await s.Db.SaveChangesAsync();
        var before = await s.OrdersSnapshotAsync();
        await Assert.ThrowsAsync<ConflictException>(() => s.Api.DeletePrescriptionAsync(DoctorActor(), 1, 1,
            new VersionRequest { RowVersion = rx.RowVersion }, default));
        Assert.Equal(before, await s.OrdersSnapshotAsync());
        Assert.Equal(1, await s.Db.HerbalPrescriptions.CountAsync());
    }

    // UTC-RX-MAIN-020: Tested method = ExaminationService.DeletePrescriptionAsync
    [Fact]
    [Trait("Module", "RX")]
    [Trait("Method", "DeletePrescriptionAsync")]
    [Trait("TestCaseId", "UTC-RX-MAIN-020")]
    public async Task DeletePrescriptionAsync_MissingPrescription_ThrowsNotFound()
    {
        using var s = await Scope.CreateAsync();
        var before = await s.OrdersSnapshotAsync();
        await Assert.ThrowsAsync<NotFoundException>(() => s.Api.DeletePrescriptionAsync(DoctorActor(), 1, 999, null, default));
        Assert.Equal(before, await s.OrdersSnapshotAsync());
    }

    // UTC-RX-MAIN-021: Tested method = ExaminationService.DeletePrescriptionAsync
    [Fact]
    [Trait("Module", "RX")]
    [Trait("Method", "DeletePrescriptionAsync")]
    [Trait("TestCaseId", "UTC-RX-MAIN-021")]
    public async Task DeletePrescriptionAsync_StaleVersion_ThrowsConflict()
    {
        using var s = await Scope.CreateAsync(); var rx = await s.PrescriptionAsync();
        var before = await s.OrdersSnapshotAsync();
        await Assert.ThrowsAsync<ConflictException>(() => s.Api.DeletePrescriptionAsync(DoctorActor(), 1, 1,
            new VersionRequest { RowVersion = Stale(rx.RowVersion) }, default));
        Assert.Equal(before, await s.OrdersSnapshotAsync());
    }

    #endregion

}

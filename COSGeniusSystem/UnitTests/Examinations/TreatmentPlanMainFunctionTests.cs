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

public sealed class TreatmentPlanMainFunctionTests : ClinicalOrderTestSupport
{
    #region ExaminationService.CreatePlanAsync

    // UTC-PLAN-MAIN-001: Tested method = ExaminationService.CreatePlanAsync
    [Fact]
    [Trait("Module", "PLAN")]
    [Trait("Method", "CreatePlanAsync")]
    [Trait("TestCaseId", "UTC-PLAN-MAIN-001")]
    public async Task CreatePlanAsync_EditableExamination_CreatesDraft()
    {
        using var s = await Scope.CreateAsync();
        var result = await s.Api.CreatePlanAsync(DoctorActor(), 1, default);
        Assert.Equal("DRAFT", result.Status); Assert.Equal("SINGLE", result.TreatmentType);
        Assert.Equal(1, result.PlannedSessionCount); Assert.Equal(s.Record.RecordId, result.RecordId);
        Assert.Empty(result.Items); Assert.False(await s.Db.TreatmentSessions.AnyAsync());
    }

    // UTC-PLAN-MAIN-002: Tested method = ExaminationService.CreatePlanAsync
    [Fact]
    [Trait("Module", "PLAN")]
    [Trait("Method", "CreatePlanAsync")]
    [Trait("TestCaseId", "UTC-PLAN-MAIN-002")]
    public async Task CreatePlanAsync_ExistingPlan_ReturnsSamePlan()
    {
        using var s = await Scope.CreateAsync(); var plan = await s.PlanAsync();
        var first = await s.Api.CreatePlanAsync(DoctorActor(), 1, default);
        var second = await s.Api.CreatePlanAsync(DoctorActor(), 1, default);
        Assert.Equal(plan.PlanId, first.PlanId); Assert.Equal(first.PlanId, second.PlanId);
        Assert.Equal(1, await s.Db.TreatmentPlans.CountAsync());
    }

    // UTC-PLAN-MAIN-003: Tested method = ExaminationService.CreatePlanAsync
    [Fact]
    [Trait("Module", "PLAN")]
    [Trait("Method", "CreatePlanAsync")]
    [Trait("TestCaseId", "UTC-PLAN-MAIN-003")]
    public async Task CreatePlanAsync_CompletedExaminationWithoutPlan_ThrowsConflict()
    {
        using var s = await Scope.CreateAsync(); await s.CompletedAsync();
        var before = await s.OrdersSnapshotAsync();
        await Assert.ThrowsAsync<ConflictException>(() => s.Api.CreatePlanAsync(DoctorActor(), 1, default));
        Assert.Equal(before, await s.OrdersSnapshotAsync());
        Assert.False(await s.Db.TreatmentPlans.AnyAsync());
    }

    #endregion

    #region ExaminationService.SavePlanAsync

    // UTC-PLAN-MAIN-004: Tested method = ExaminationService.SavePlanAsync
    [Fact]
    [Trait("Module", "PLAN")]
    [Trait("Method", "SavePlanAsync")]
    [Trait("TestCaseId", "UTC-PLAN-MAIN-004")]
    public async Task SavePlanAsync_ValidCourse_SavesServicesAndSessionDetails()
    {
        using var s = await Scope.CreateAsync(); var plan = await s.PlanAsync();
        var request = Scope.Treatment(plan); request.TreatmentType = "COURSE"; request.PlannedSessionCount = 2;
        request.Items[0].PlannedQuantity = 2;
        request.Sessions = [new() { SessionNumber = 1, ServiceIds = [1] }, new() { SessionNumber = 2, ServiceIds = [1] }];
        var result = await s.Api.SavePlanAsync(DoctorActor(), 1, 1, request, default);
        Assert.Equal("COURSE", result.TreatmentType); Assert.Equal(2, result.PlannedSessionCount);
        Assert.Equal(2, Assert.Single(result.Items).PlannedQuantity); Assert.Equal(2, result.Sessions.Count);
        s.Db.ChangeTracker.Clear();
        var saved = await s.Db.TreatmentPlans.SingleAsync();
        Assert.Equal("Edited treatment", saved.PlanName); Assert.NotNull(saved.SessionDetailsJson);
        Assert.False(await s.Db.TreatmentSessions.AnyAsync());
    }

    // UTC-PLAN-MAIN-005: Tested method = ExaminationService.SavePlanAsync
    [Fact]
    [Trait("Module", "PLAN")]
    [Trait("Method", "SavePlanAsync")]
    [Trait("TestCaseId", "UTC-PLAN-MAIN-005")]
    public async Task SavePlanAsync_ConfirmedPlan_RemainsEditableUntilCompletion()
    {
        using var s = await Scope.CreateAsync(); var plan = await s.PlanAsync();
        plan.Status = "CONFIRMED"; plan.ConfirmedAt = DateTime.UtcNow; await s.Db.SaveChangesAsync();
        var result = await s.Api.SavePlanAsync(DoctorActor(), 1, 1, Scope.Treatment(plan), default);
        Assert.Equal("Edited treatment", result.PlanName); Assert.Equal("CONFIRMED", result.Status);
        await s.CompletedAsync();
        var before = await s.OrdersSnapshotAsync();
        await Assert.ThrowsAsync<ConflictException>(() => s.Api.SavePlanAsync(DoctorActor(), 1, 1, Scope.Treatment(plan), default));
        Assert.Equal(before, await s.OrdersSnapshotAsync());
    }

    // UTC-PLAN-MAIN-006: Tested method = ExaminationService.SavePlanAsync
    [Fact]
    [Trait("Module", "PLAN")]
    [Trait("Method", "SavePlanAsync")]
    [Trait("TestCaseId", "UTC-PLAN-MAIN-006")]
    public async Task SavePlanAsync_InconsistentSessionQuantities_ThrowsBadRequest()
    {
        using var s = await Scope.CreateAsync(); var plan = await s.PlanAsync();
        var request = Scope.Treatment(plan); request.TreatmentType = "COURSE"; request.PlannedSessionCount = 2;
        request.Items[0].PlannedQuantity = 2;
        request.Sessions = [new() { SessionNumber = 1, ServiceIds = [1] }];
        var before = await s.OrdersSnapshotAsync();
        await Assert.ThrowsAsync<BadRequestException>(() => s.Api.SavePlanAsync(DoctorActor(), 1, 1, request, default));
        Assert.Equal(before, await s.OrdersSnapshotAsync());
        s.Db.ChangeTracker.Clear();
        Assert.Equal(1, (await s.Db.TreatmentPlans.SingleAsync()).PlannedSessionCount);
    }

    // UTC-PLAN-MAIN-007: Tested method = ExaminationService.SavePlanAsync
    [Fact]
    [Trait("Module", "PLAN")]
    [Trait("Method", "SavePlanAsync")]
    [Trait("TestCaseId", "UTC-PLAN-MAIN-007")]
    public async Task SavePlanAsync_InactiveService_ThrowsBadRequest()
    {
        using var s = await Scope.CreateAsync(); var plan = await s.PlanAsync();
        (await s.Db.Services.SingleAsync()).IsActive = false; await s.Db.SaveChangesAsync();
        var before = await s.OrdersSnapshotAsync();
        await Assert.ThrowsAsync<BadRequestException>(() => s.Api.SavePlanAsync(DoctorActor(), 1, 1, Scope.Treatment(plan), default));
        Assert.Equal(before, await s.OrdersSnapshotAsync());
    }

    // UTC-PLAN-MAIN-008: Tested method = ExaminationService.SavePlanAsync
    [Fact]
    [Trait("Module", "PLAN")]
    [Trait("Method", "SavePlanAsync")]
    [Trait("TestCaseId", "UTC-PLAN-MAIN-008")]
    public async Task SavePlanAsync_StaleVersion_ThrowsConflict()
    {
        using var s = await Scope.CreateAsync(); var plan = await s.PlanAsync();
        var request = Scope.Treatment(plan); request.RowVersion = Stale(plan.RowVersion);
        var before = await s.OrdersSnapshotAsync();
        await Assert.ThrowsAsync<ConflictException>(() => s.Api.SavePlanAsync(DoctorActor(), 1, 1, request, default));
        Assert.Equal(before, await s.OrdersSnapshotAsync());
    }

    #endregion

    #region ExaminationService.ConfirmPlanAsync

    // UTC-PLAN-MAIN-009: Tested method = ExaminationService.ConfirmPlanAsync
    [Fact]
    [Trait("Module", "PLAN")]
    [Trait("Method", "ConfirmPlanAsync")]
    [Trait("TestCaseId", "UTC-PLAN-MAIN-009")]
    public async Task ConfirmPlanAsync_ValidPlan_ConfirmsWithoutCreatingSessions()
    {
        using var s = await Scope.CreateAsync(); var plan = await s.PlanAsync();
        var result = await s.Api.ConfirmPlanAsync(DoctorActor(), 1, 1, new VersionRequest { RowVersion = plan.RowVersion }, default);
        Assert.Equal("CONFIRMED", result.Status); Assert.NotNull(result.ConfirmedAt);
        s.Db.ChangeTracker.Clear();
        Assert.Equal("CONFIRMED", (await s.Db.TreatmentPlans.SingleAsync()).Status);
        Assert.False(await s.Db.TreatmentSessions.AnyAsync()); Assert.False(await s.Db.Invoices.AnyAsync());
    }

    // UTC-PLAN-MAIN-010: Tested method = ExaminationService.ConfirmPlanAsync
    [Fact]
    [Trait("Module", "PLAN")]
    [Trait("Method", "ConfirmPlanAsync")]
    [Trait("TestCaseId", "UTC-PLAN-MAIN-010")]
    public async Task ConfirmPlanAsync_EmptyPlan_ThrowsBadRequest()
    {
        using var s = await Scope.CreateAsync(); var plan = await s.PlanAsync(withItems: false);
        var before = await s.OrdersSnapshotAsync();
        await Assert.ThrowsAsync<BadRequestException>(() => s.Api.ConfirmPlanAsync(DoctorActor(), 1, 1,
            new VersionRequest { RowVersion = plan.RowVersion }, default));
        Assert.Equal(before, await s.OrdersSnapshotAsync());
        s.Db.ChangeTracker.Clear();
        Assert.Equal("DRAFT", (await s.Db.TreatmentPlans.SingleAsync()).Status);
    }

    // UTC-PLAN-MAIN-011: Tested method = ExaminationService.ConfirmPlanAsync
    [Fact]
    [Trait("Module", "PLAN")]
    [Trait("Method", "ConfirmPlanAsync")]
    [Trait("TestCaseId", "UTC-PLAN-MAIN-011")]
    public async Task ConfirmPlanAsync_AlreadyConfirmed_IsIdempotent()
    {
        using var s = await Scope.CreateAsync(); var plan = await s.PlanAsync();
        var request = new VersionRequest { RowVersion = plan.RowVersion };
        var first = await s.Api.ConfirmPlanAsync(DoctorActor(), 1, 1, request, default);
        var second = await s.Api.ConfirmPlanAsync(DoctorActor(), 1, 1, request, default);
        Assert.Equal(first.ConfirmedAt, second.ConfirmedAt); Assert.False(await s.Db.TreatmentSessions.AnyAsync());
    }

    // UTC-PLAN-MAIN-012: Tested method = ExaminationService.ConfirmPlanAsync
    [Fact]
    [Trait("Module", "PLAN")]
    [Trait("Method", "ConfirmPlanAsync")]
    [Trait("TestCaseId", "UTC-PLAN-MAIN-012")]
    public async Task ConfirmPlanAsync_StaleVersion_ThrowsConflict()
    {
        using var s = await Scope.CreateAsync(); var plan = await s.PlanAsync();
        var before = await s.OrdersSnapshotAsync();
        await Assert.ThrowsAsync<ConflictException>(() => s.Api.ConfirmPlanAsync(DoctorActor(), 1, 1,
            new VersionRequest { RowVersion = Stale(plan.RowVersion) }, default));
        Assert.Equal(before, await s.OrdersSnapshotAsync());
    }

    #endregion

    #region ExaminationService.DeletePlanAsync

    // UTC-PLAN-MAIN-013: Tested method = ExaminationService.DeletePlanAsync
    [Fact]
    [Trait("Module", "PLAN")]
    [Trait("Method", "DeletePlanAsync")]
    [Trait("TestCaseId", "UTC-PLAN-MAIN-013")]
    public async Task DeletePlanAsync_Draft_RemovesPlanAndDetails()
    {
        using var s = await Scope.CreateAsync(); var plan = await s.PlanAsync();
        await s.Api.DeletePlanAsync(DoctorActor(), 1, 1, new VersionRequest { RowVersion = plan.RowVersion }, default);
        s.Db.ChangeTracker.Clear();
        Assert.False(await s.Db.TreatmentPlans.AnyAsync()); Assert.False(await s.Db.TreatmentPlanDetails.AnyAsync());
        Assert.True(await s.Db.Services.AnyAsync());
        Assert.True(await s.Db.MedicalRecords.AnyAsync());
    }

    // UTC-PLAN-MAIN-014: Tested method = ExaminationService.DeletePlanAsync
    [Fact]
    [Trait("Module", "PLAN")]
    [Trait("Method", "DeletePlanAsync")]
    [Trait("TestCaseId", "UTC-PLAN-MAIN-014")]
    public async Task DeletePlanAsync_ConfirmedPlan_ThrowsConflict()
    {
        using var s = await Scope.CreateAsync(); var plan = await s.PlanAsync();
        plan.Status = "CONFIRMED"; await s.Db.SaveChangesAsync();
        var before = await s.OrdersSnapshotAsync();
        await Assert.ThrowsAsync<ConflictException>(() => s.Api.DeletePlanAsync(DoctorActor(), 1, 1,
            new VersionRequest { RowVersion = plan.RowVersion }, default));
        Assert.Equal(before, await s.OrdersSnapshotAsync());
        Assert.Equal(1, await s.Db.TreatmentPlans.CountAsync());
    }

    // UTC-PLAN-MAIN-015: Tested method = ExaminationService.DeletePlanAsync
    [Fact]
    [Trait("Module", "PLAN")]
    [Trait("Method", "DeletePlanAsync")]
    [Trait("TestCaseId", "UTC-PLAN-MAIN-015")]
    public async Task DeletePlanAsync_ExistingSession_ThrowsConflict()
    {
        using var s = await Scope.CreateAsync(); var plan = await s.PlanAsync();
        s.Db.TreatmentSessions.Add(new TreatmentSession { Plan = plan, SessionNumber = 1,
            ScheduledDate = Today, Status = "REQUESTED" });
        await s.Db.SaveChangesAsync();
        var before = await s.OrdersSnapshotAsync();
        await Assert.ThrowsAsync<ConflictException>(() => s.Api.DeletePlanAsync(DoctorActor(), 1, 1,
            new VersionRequest { RowVersion = plan.RowVersion }, default));
        Assert.Equal(before, await s.OrdersSnapshotAsync());
        Assert.Equal(1, await s.Db.TreatmentPlans.CountAsync());
    }

    // UTC-PLAN-MAIN-016: Tested method = ExaminationService.DeletePlanAsync
    [Fact]
    [Trait("Module", "PLAN")]
    [Trait("Method", "DeletePlanAsync")]
    [Trait("TestCaseId", "UTC-PLAN-MAIN-016")]
    public async Task DeletePlanAsync_StaleVersion_ThrowsConflict()
    {
        using var s = await Scope.CreateAsync(); var plan = await s.PlanAsync();
        var before = await s.OrdersSnapshotAsync();
        await Assert.ThrowsAsync<ConflictException>(() => s.Api.DeletePlanAsync(DoctorActor(), 1, 1,
            new VersionRequest { RowVersion = Stale(plan.RowVersion) }, default));
        Assert.Equal(before, await s.OrdersSnapshotAsync());
    }

    #endregion

}

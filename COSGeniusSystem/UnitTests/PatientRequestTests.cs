using System.Security.Claims;
using COSGeniusSystem.Commons;
using COSGeniusSystem.DTOs;
using COSGeniusSystem.Exceptions;
using COSGeniusSystem.Models;
using COSGeniusSystem.Services;
using Microsoft.EntityFrameworkCore;

namespace COSGeniusSystem.UnitTests;

public class PatientRequestTests
{
    private static ClaimsPrincipal Reception => new(new ClaimsIdentity([
        new Claim(ClaimTypes.NameIdentifier, "1"),
        new Claim(ClaimTypes.Role, RoleConstants.ReceptionStaff)
    ], "test"));

    [Theory]
    [InlineData("WAITING", false, false)]
    [InlineData("IN_PROGRESS", false, false)]
    [InlineData("REQUESTED", true, false)]
    [InlineData("IN_PROGRESS", true, false)]
    [InlineData("WAITING", false, true)]
    [InlineData("IN_PROGRESS", false, true)]
    [InlineData("REQUESTED", true, true)]
    [InlineData("IN_PROGRESS", true, true)]
    public async Task ActiveRequestBlocksEitherCreationPath(string status, bool existingTreatment, bool createTreatment)
    {
        await using var db = SchemaFixture.Create();
        var record = SchemaFixture.Record();
        record.Doctor.User!.IsActive = true;
        record.Doctor.User.UserRoles.Add(new UserRole { Role = new Role { RoleCode = RoleConstants.Doctor, RoleName = "Bác sĩ" } });
        var plan = SchemaFixture.Plan(record);
        db.TreatmentPlans.Add(plan);
        if (existingTreatment)
            db.TreatmentSessions.Add(new TreatmentSession { SessionId = 1, Plan = plan, SessionNumber = 1, Status = status });
        else
            record.Registration.Status = status;
        // The next planned session must remain untouched when session 1/3 is active.
        db.TreatmentSessions.Add(new TreatmentSession { SessionId = 2, Plan = plan, SessionNumber = 2, Status = "PLANNED" });
        await db.SaveChangesAsync();

        var error = createTreatment
            ? await Assert.ThrowsAsync<ConflictException>(() => new TreatmentWorkflowService(db).RequestAsync(Reception, 1,
                new RequestTreatment { RowVersion = new byte[8], AssignedDoctorId = 1, Shift = "AFTERNOON", ScheduledDate = DateOnly.FromDateTime(DateTime.UtcNow.AddHours(7)) }, default))
            : await Assert.ThrowsAsync<ConflictException>(() => new ExaminationService(db).CreateAsync(Reception,
                new CreateRegistrationRequest { PatientId = 1, DoctorId = 1, Session = "AFTERNOON" }, default));

        Assert.Equal(409, error.StatusCode);
        Assert.Contains(existingTreatment ? "treatment" : "examination", error.Message);
        Assert.Equal(1, await db.ExaminationRegistrations.CountAsync());
        Assert.Equal(existingTreatment ? 2 : 1, await db.TreatmentSessions.CountAsync());
        Assert.Equal("PLANNED", (await db.TreatmentSessions.AsNoTracking().SingleAsync(s => s.SessionId == 2)).Status);
        Assert.Empty(await db.ExaminationQueueCounters.ToListAsync());
    }

    [Theory]
    [InlineData("COMPLETED")]
    [InlineData("CANCELLED")]
    [InlineData("PLANNED")]
    public async Task TerminalAndPlannedRequestsDoNotBlockPatient(string status)
    {
        await using var db = SchemaFixture.Create();
        var record = SchemaFixture.Record();
        record.Registration.Status = status == "PLANNED" ? "COMPLETED" : status;
        var plan = SchemaFixture.Plan(record);
        db.TreatmentSessions.Add(new TreatmentSession { Plan = plan, SessionNumber = 1, Status = status });
        await db.SaveChangesAsync();
        await PatientRequestGuard.EnsureAvailableAsync(db, 1, default);
    }

    [Fact]
    public async Task RequestsOfAnotherPatientDoNotBlockPatient()
    {
        await using var db = SchemaFixture.Create();
        var record = SchemaFixture.Record();
        record.Registration.Status = "WAITING";
        db.TreatmentSessions.Add(new TreatmentSession { Plan = SchemaFixture.Plan(record), SessionNumber = 1, Status = "REQUESTED" });
        await db.SaveChangesAsync();
        await PatientRequestGuard.EnsureAvailableAsync(db, 2, default);
    }

    [Fact]
    public async Task ActiveTreatmentInAnotherPlanDoesNotBlockEligibleTreatment()
    {
        await using var db = SchemaFixture.Create();
        var record = SchemaFixture.Record();
        record.Doctor.User!.IsActive = true;
        record.Doctor.User.UserRoles.Add(new UserRole { Role = new Role { RoleCode = RoleConstants.Doctor, RoleName = "Bác sĩ" } });
        var first = SchemaFixture.Plan(record);
        db.TreatmentSessions.Add(new TreatmentSession { Plan = first, SessionNumber = 1, Status = "REQUESTED" });
        var second = SchemaFixture.Plan(record);
        second.PlanId = 2;
        second.TreatmentType = "SINGLE";
        db.TreatmentPlans.Add(second);
        db.TreatmentPlanDetails.Add(new TreatmentPlanDetail { Plan = second, ServiceId = 1,
            ServiceNameSnapshot = "Châm cứu", UnitSnapshot = "buổi", PlannedQuantity = 1 });
        await db.SaveChangesAsync();

        await new TreatmentWorkflowService(db).RequestAsync(Reception, 2,
            new RequestTreatment { RowVersion = new byte[8], AssignedDoctorId = 1, Shift = "AFTERNOON",
                ScheduledDate = DateOnly.FromDateTime(DateTime.UtcNow.AddHours(7)).AddDays(1) }, default);
        Assert.True(await db.TreatmentSessions.AnyAsync(s => s.PlanId == 2 && s.Status == "REQUESTED"));
    }

    [Theory]
    [InlineData("COMPLETED")]
    [InlineData("CANCELLED")]
    public async Task CanRegisterAgainAfterPreviousRequestEnds(string status)
    {
        await using var db = SchemaFixture.Create();
        var record = SchemaFixture.Record();
        record.Registration.Status = status;
        record.Doctor.User!.IsActive = true;
        record.Doctor.User.UserRoles.Add(new UserRole { Role = new Role { RoleCode = RoleConstants.Doctor, RoleName = "Bác sĩ" } });
        db.MedicalRecords.Add(record);
        await db.SaveChangesAsync();

        var result = await new ExaminationService(db).CreateAsync(Reception,
            new CreateRegistrationRequest { PatientId = 1, DoctorId = 1, Session = "AFTERNOON" }, default);

        Assert.Equal("WAITING", result.Status);
        Assert.Equal(2, await db.ExaminationRegistrations.CountAsync());
    }
}

using System.Security.Claims;
using COSGeniusSystem.Commons;
using COSGeniusSystem.DTOs;
using COSGeniusSystem.Exceptions;
using COSGeniusSystem.Models;
using COSGeniusSystem.Services;
using Microsoft.EntityFrameworkCore;

namespace COSGeniusSystem.UnitTests;

public class DoctorAvailabilityTests
{
    private static ClaimsPrincipal Actor() => new(new ClaimsIdentity([
        new Claim(ClaimTypes.NameIdentifier, "1"), new Claim(ClaimTypes.Role, RoleConstants.Doctor)
    ], "test"));

    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public async Task BusyDoctorCannotStartAnotherExaminationOrTreatment(bool treating, bool startTreatment)
    {
        await using var db = SchemaFixture.Create();
        var current = SchemaFixture.Record();
        current.Doctor.User!.IsActive = true;
        current.Doctor.User.UserRoles.Add(new UserRole { Role = new Role { RoleCode = RoleConstants.Doctor, RoleName = "Bác sĩ" } });
        db.MedicalRecords.Add(current);
        var waiting = SchemaFixture.Registration(2);
        waiting.Doctor = current.Doctor;
        db.ExaminationRegistrations.Add(waiting);
        var plan = SchemaFixture.Plan(current);
        db.TreatmentPlans.Add(plan);
        var nextSession = new TreatmentSession { SessionId = 2, Plan = plan, SessionNumber = 2,
            AssignedDoctor = current.Doctor, Status = "REQUESTED", ScheduledDate = DateOnly.FromDateTime(DateTime.UtcNow.AddHours(7)) };
        db.TreatmentSessions.Add(nextSession);
        if (treating)
            db.TreatmentSessions.Add(new TreatmentSession { SessionId = 1, Plan = plan, SessionNumber = 1,
                AssignedDoctor = current.Doctor, Status = "IN_PROGRESS" });
        else
        {
            current.Status = "IN_PROGRESS";
            current.Registration.Status = "IN_PROGRESS";
        }
        await db.SaveChangesAsync();

        if (startTreatment)
            await Assert.ThrowsAsync<ConflictException>(() => new TreatmentWorkflowService(db)
                .StartAsync(Actor(), 2, new VersionRequest { RowVersion = new byte[8] }, default));
        else
            await Assert.ThrowsAsync<ConflictException>(() => new ExaminationService(db).StartAsync(Actor(), 2, default));

        Assert.Equal("WAITING", (await db.ExaminationRegistrations.AsNoTracking().SingleAsync(r => r.RegistrationId == 2)).Status);
        Assert.Equal("REQUESTED", (await db.TreatmentSessions.AsNoTracking().SingleAsync(s => s.SessionId == 2)).Status);
        Assert.False(await db.MedicalRecords.AnyAsync(r => r.RegistrationId == 2));
    }

    [Theory]
    [InlineData("COMPLETED")]
    [InlineData("CANCELLED")]
    [InlineData("WAITING")]
    public async Task InactiveCasesDoNotBlockDoctor(string status)
    {
        await using var db = SchemaFixture.Create();
        var record = SchemaFixture.Record();
        record.Status = status;
        record.Registration.Status = status;
        db.MedicalRecords.Add(record);
        await db.SaveChangesAsync();
        await DoctorAvailability.EnsureAvailableAsync(db, record.DoctorId, default);
    }

    [Fact]
    public async Task BusyDoctorDoesNotBlockAnotherDoctor()
    {
        await using var db = SchemaFixture.Create();
        var record = SchemaFixture.Record();
        record.Status = "IN_PROGRESS";
        record.Registration.Status = "IN_PROGRESS";
        db.MedicalRecords.Add(record);
        await db.SaveChangesAsync();
        await DoctorAvailability.EnsureAvailableAsync(db, 2, default);
    }
}

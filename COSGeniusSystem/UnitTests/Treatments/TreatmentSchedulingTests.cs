using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using COSGeniusSystem.Commons;
using COSGeniusSystem.DTOs;
using COSGeniusSystem.Models;
using COSGeniusSystem.Services;

namespace COSGeniusSystem.UnitTests;

public class TreatmentSchedulingTests
{
    [Theory]
    [InlineData("MORNING", true)]
    [InlineData("AFTERNOON", true)]
    [InlineData("", false)]
    [InlineData("EVENING", false)]
    public void RequestRequiresSupportedShift(string shift, bool valid)
    {
        var request = new RequestTreatment { AssignedDoctorId = 1, RowVersion = new byte[8], ScheduledDate = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(10), Shift = shift };
        Assert.Equal(valid, Validator.TryValidateObject(request, new ValidationContext(request), [], true));
    }

    [Fact]
    public async Task WorkloadSeparatesDatesShiftsDoctorsAndStates()
    {
        using var db = SchemaFixture.Create();
        var record = SchemaFixture.Record();
        record.Doctor.User!.IsActive = true;
        record.Doctor.User.UserRoles.Add(new UserRole { Role = new Role { RoleId = 1, RoleCode = RoleConstants.Doctor, RoleName = "Doctor" } });
        db.MedicalRecords.Add(record);
        await db.SaveChangesAsync();
        var date = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(10);
        foreach (var status in new[] { "REQUESTED", "IN_PROGRESS", "COMPLETED", "CANCELLED", "PLANNED" })
            db.TreatmentSessions.Add(new TreatmentSession { PlanId = 999, AssignedDoctorId = record.DoctorId, ScheduledDate = date, Shift = "MORNING", Status = status });
        db.TreatmentSessions.Add(new TreatmentSession { PlanId = 999, AssignedDoctorId = record.DoctorId, ScheduledDate = date, Shift = "AFTERNOON", Status = "REQUESTED" });
        db.TreatmentSessions.Add(new TreatmentSession { PlanId = 999, AssignedDoctorId = record.DoctorId, ScheduledDate = date.AddDays(1), Shift = "MORNING", Status = "REQUESTED" });
        db.TreatmentSessions.Add(new TreatmentSession { PlanId = 999, AssignedDoctorId = record.DoctorId, ScheduledDate = date, Status = "REQUESTED" });
        await db.SaveChangesAsync();
        var actor = new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, "1"), new Claim(ClaimTypes.Role, RoleConstants.ReceptionStaff)], "test"));
        var service = new TreatmentWorkflowService(db);
        var query = new TreatmentWorkloadQuery { ScheduledDate = date, Shift = "MORNING", DoctorId = record.DoctorId };
        var result = Assert.Single(await service.WorkloadAsync(actor, query, default));
        Assert.Equal(3, result.AssignedCount);
        Assert.Equal(1, result.WaitingCount);
        Assert.Equal(1, result.InProgressCount);
        Assert.Equal(1, result.CompletedCount);
        query.DoctorId = int.MaxValue;
        Assert.Empty(await service.WorkloadAsync(actor, query, default));
        query.DoctorId = record.DoctorId;
        query.ScheduledDate = date.AddDays(2);
        Assert.Equal(0, Assert.Single(await service.WorkloadAsync(actor, query, default)).AssignedCount);
    }
}

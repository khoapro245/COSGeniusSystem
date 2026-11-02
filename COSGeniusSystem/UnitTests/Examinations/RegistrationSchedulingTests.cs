using System.ComponentModel.DataAnnotations;
using COSGeniusSystem.DTOs;
using COSGeniusSystem.Services;
using System.Security.Claims;

namespace COSGeniusSystem.UnitTests;

public class RegistrationSchedulingTests
{
    [Theory]
    [InlineData("MORNING", true)]
    [InlineData("AFTERNOON", true)]
    [InlineData("UNKNOWN", false)]
    [InlineData("", false)]
    [InlineData("EVENING", false)]
    public void RegistrationRequiresExplicitSupportedSession(string session, bool expected)
    {
        var request = new CreateRegistrationRequest { PatientId = 1, DoctorId = 1, Session = session };
        Assert.Equal(expected, Validator.TryValidateObject(request, new ValidationContext(request), [], true));
    }

    [Fact]
    public async Task WorkloadIncludesZeroLoadAndExcludesCancelledAndOtherSessions()
    {
        using var db = SchemaFixture.Create();
        var record = SchemaFixture.Record();
        record.Doctor.User!.IsActive = true;
        record.Doctor.User.UserRoles.Add(new COSGeniusSystem.Models.UserRole
        {
            Role = new COSGeniusSystem.Models.Role { RoleId = 1, RoleCode = "DOCTOR", RoleName = "Doctor" }
        });
        record.Registration.Session = "MORNING";
        db.MedicalRecords.Add(record);
        await db.SaveChangesAsync();
        var actor = new ClaimsPrincipal(new ClaimsIdentity(new[]
        {
            new Claim(ClaimTypes.NameIdentifier, "1"),
            new Claim(ClaimTypes.Role, COSGeniusSystem.Commons.RoleConstants.ReceptionStaff)
        }, "test"));
        var service = new ExaminationService(db);
        var query = new DoctorWorkloadQuery { BusinessDate = record.Registration.BusinessDate, Session = "MORNING" };
        var result = await service.DoctorWorkloadAsync(actor, query, default);
        Assert.Equal(1, Assert.Single(result).AssignedCount);
        record.Registration.Status = "CANCELLED";
        await db.SaveChangesAsync();
        Assert.Equal(0, Assert.Single(await service.DoctorWorkloadAsync(actor, query, default)).AssignedCount);
        record.Registration.Status = "COMPLETED";
        record.Registration.Session = "AFTERNOON";
        await db.SaveChangesAsync();
        Assert.Equal(0, Assert.Single(await service.DoctorWorkloadAsync(actor, query, default)).AssignedCount);
    }
}

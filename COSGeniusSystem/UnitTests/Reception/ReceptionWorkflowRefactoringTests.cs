using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using COSGeniusSystem.Commons;
using COSGeniusSystem.DTOs;
using COSGeniusSystem.Exceptions;
using COSGeniusSystem.Models;
using COSGeniusSystem.Services;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace COSGeniusSystem.UnitTests;

public class ReceptionWorkflowRefactoringTests
{
    private static ClaimsPrincipal ReceptionActor => new(new ClaimsIdentity(new[]
    {
        new Claim(ClaimTypes.NameIdentifier, "1"),
        new Claim(ClaimTypes.Role, RoleConstants.ReceptionStaff)
    }, "test"));

    [Fact]
    public async Task CancelExamTicket_WhenWaiting_SucceedsAndSetsAudit()
    {
        using var db = SchemaFixture.Create();
        var record = SchemaFixture.Record();
        record.Registration.Status = "WAITING";
        record.Registration.Note = "Ghi chú ban đầu";
        db.MedicalRecords.Add(record);
        await db.SaveChangesAsync();

        var service = new ExaminationService(db);
        var result = await service.CancelAsync(ReceptionActor, record.RegistrationId,
            new CancelTicketRequest { CancelReason = "Bệnh nhân có việc bận xin về" }, default);

        Assert.NotNull(result);
        Assert.Equal("CANCELLED", result.Status);

        var saved = await db.ExaminationRegistrations.SingleAsync(r => r.RegistrationId == record.RegistrationId);
        Assert.Equal("CANCELLED", saved.Status);
        Assert.NotNull(saved.CancelledAt);
        Assert.Contains("Bệnh nhân có việc bận xin về", saved.Note);
    }

    [Fact]
    public async Task CancelExamTicket_WhenInProgress_ThrowsBadRequestException()
    {
        using var db = SchemaFixture.Create();
        var record = SchemaFixture.Record();
        record.Registration.Status = "IN_PROGRESS";
        db.MedicalRecords.Add(record);
        await db.SaveChangesAsync();

        var service = new ExaminationService(db);
        var ex = await Assert.ThrowsAsync<BadRequestException>(() =>
            service.CancelAsync(ReceptionActor, record.RegistrationId,
                new CancelTicketRequest { CancelReason = "Hủy ngang" }, default));

        Assert.Contains("Không thể hủy phiếu khi bác sĩ/KTV đã bắt đầu thực hiện", ex.Message);
    }

    [Fact]
    public async Task CancelExamTicket_WithoutReason_ThrowsBadRequestException()
    {
        using var db = SchemaFixture.Create();
        var record = SchemaFixture.Record();
        record.Registration.Status = "WAITING";
        db.MedicalRecords.Add(record);
        await db.SaveChangesAsync();

        var service = new ExaminationService(db);
        await Assert.ThrowsAsync<BadRequestException>(() =>
            service.CancelAsync(ReceptionActor, record.RegistrationId,
                new CancelTicketRequest { CancelReason = "" }, default));
    }

    [Fact]
    public async Task ListAsync_MultiLevelOrdering_OrdersCorrectly()
    {
        using var db = SchemaFixture.Create();
        var doctor = new Doctor { DoctorId = 1, FullName = "Bác sĩ Test", IsActive = true };
        var patient = new Patient { PatientId = 1, FullName = "Bệnh nhân Test", Phone = "0901234567" };
        var user = new User { UserId = 1, Username = "staff", FullName = "Staff", PasswordHash = "hash", IsActive = true };
        db.Doctors.Add(doctor);
        db.Patients.Add(patient);
        db.Users.Add(user);

        var date = DateOnly.FromDateTime(DateTime.UtcNow.AddHours(7));
        var r1 = new ExaminationRegistration
        {
            RegistrationId = 101, Doctor = doctor, Patient = patient, RegisteredByNavigation = user,
            BusinessDate = date, Status = "COMPLETED", IsPriority = true,
            RegisteredAt = DateTime.UtcNow.AddMinutes(-30), QueueNumber = 1
        };
        var r2 = new ExaminationRegistration
        {
            RegistrationId = 102, Doctor = doctor, Patient = patient, RegisteredByNavigation = user,
            BusinessDate = date, Status = "IN_PROGRESS", IsPriority = false,
            RegisteredAt = DateTime.UtcNow.AddMinutes(-20), QueueNumber = 2
        };
        var r3 = new ExaminationRegistration
        {
            RegistrationId = 103, Doctor = doctor, Patient = patient, RegisteredByNavigation = user,
            BusinessDate = date, Status = "WAITING", IsPriority = true,
            RegisteredAt = DateTime.UtcNow.AddMinutes(-10), QueueNumber = 3
        };
        var r4 = new ExaminationRegistration
        {
            RegistrationId = 104, Doctor = doctor, Patient = patient, RegisteredByNavigation = user,
            BusinessDate = date, Status = "WAITING", IsPriority = false,
            RegisteredAt = DateTime.UtcNow.AddMinutes(-15), QueueNumber = 4
        };
        db.ExaminationRegistrations.AddRange(r1, r2, r3, r4);
        await db.SaveChangesAsync();

        var service = new ExaminationService(db);
        var result = await service.ListAsync(ReceptionActor, new RegistrationQuery { PageNumber = 1, PageSize = 10 }, false, default);

        Assert.Equal(4, result.Items.Count);
        // 1st: IN_PROGRESS (r2)
        Assert.Equal(102, result.Items[0].RegistrationId);
        // 2nd: WAITING Priority (r3)
        Assert.Equal(103, result.Items[1].RegistrationId);
        // 3rd: WAITING Non-priority FIFO (r4)
        Assert.Equal(104, result.Items[2].RegistrationId);
        // 4th: COMPLETED (r1)
        Assert.Equal(101, result.Items[3].RegistrationId);

        // Continuous STT
        Assert.Equal(1, result.Items[0].QueueNumber);
        Assert.Equal(2, result.Items[1].QueueNumber);
        Assert.Equal(3, result.Items[2].QueueNumber);
        Assert.Equal(4, result.Items[3].QueueNumber);
    }

    [Fact]
    public async Task CancelTreatmentSession_WhenRequested_Succeeds()
    {
        using var db = SchemaFixture.Create();
        var record = SchemaFixture.Record();
        var plan = SchemaFixture.Plan(record);
        db.TreatmentPlans.Add(plan);
        var session = new TreatmentSession
        {
            SessionId = 50, Plan = plan, SessionNumber = 1,
            ScheduledDate = record.Registration.BusinessDate,
            Status = "REQUESTED", AssignedDoctor = record.Doctor,
            RequestedAt = DateTime.UtcNow
        };
        db.TreatmentSessions.Add(session);
        await db.SaveChangesAsync();

        var service = new TreatmentWorkflowService(db);
        var result = await service.CancelSessionTicketAsync(ReceptionActor, 50,
            new CancelTicketRequest { CancelReason = "Khách xin dời lịch" }, default);

        Assert.NotNull(result);
        Assert.Equal("CANCELLED", result.Session.Status);

        var saved = await db.TreatmentSessions.SingleAsync(s => s.SessionId == 50);
        Assert.Equal("CANCELLED", saved.Status);
        Assert.Contains("Khách xin dời lịch", saved.ResultNote);
    }

    [Fact]
    public async Task CancelTreatmentSession_WhenInProgress_ThrowsBadRequestException()
    {
        using var db = SchemaFixture.Create();
        var record = SchemaFixture.Record();
        var plan = SchemaFixture.Plan(record);
        db.TreatmentPlans.Add(plan);
        var session = new TreatmentSession
        {
            SessionId = 51, Plan = plan, SessionNumber = 1,
            ScheduledDate = record.Registration.BusinessDate,
            Status = "IN_PROGRESS", AssignedDoctor = record.Doctor,
            StartedAt = DateTime.UtcNow
        };
        db.TreatmentSessions.Add(session);
        await db.SaveChangesAsync();

        var service = new TreatmentWorkflowService(db);
        var ex = await Assert.ThrowsAsync<BadRequestException>(() =>
            service.CancelSessionTicketAsync(ReceptionActor, 51,
                new CancelTicketRequest { CancelReason = "Hủy buổi trị liệu" }, default));

        Assert.Contains("Không thể hủy phiếu khi bác sĩ/KTV đã bắt đầu thực hiện", ex.Message);
    }

    [Theory]
    [InlineData("0901234567", true)]
    [InlineData("0389998888", true)]
    [InlineData("0851234567", true)]
    [InlineData("1234567890", false)] // doesn't start with 0
    [InlineData("090123456", false)]   // 9 digits
    [InlineData("09012345678", false)]  // 11 digits
    [InlineData("abcdefghij", false)]
    public void CreatePatientDto_PhoneValidation(string phone, bool expectedValid)
    {
        var dto = new CreatePatientDto
        {
            FullName = "Nguyen Van Test",
            Phone = phone
        };
        var errors = new List<ValidationResult>();
        var isValid = Validator.TryValidateObject(dto, new ValidationContext(dto), errors, true);
        Assert.Equal(expectedValid, isValid);
    }

    [Theory]
    [InlineData("0901234567", true)]
    [InlineData("0389998888", true)]
    [InlineData("0701122334", true)]
    [InlineData("1234567890", false)]
    [InlineData("09012345", false)]
    [InlineData("abcdefghij", false)]
    public void UpdatePatientDto_PhoneValidation(string phone, bool expectedValid)
    {
        var dto = new UpdatePatientDto
        {
            FullName = "Nguyen Van Test",
            Phone = phone
        };
        var errors = new List<ValidationResult>();
        var isValid = Validator.TryValidateObject(dto, new ValidationContext(dto), errors, true);
        Assert.Equal(expectedValid, isValid);
    }

    [Theory]
    [InlineData("123456789", true)]
    [InlineData("123456789012", true)]
    [InlineData(null, true)]
    [InlineData("", true)]
    [InlineData("12345", false)]
    [InlineData("1234567890123", false)]
    [InlineData("12345abc9012", false)]
    public void UpdatePatientDto_IdentityCardValidation(string? identityCard, bool expectedValid)
    {
        var dto = new UpdatePatientDto
        {
            FullName = "Nguyen Van Test",
            Phone = "0901234567",
            IdentityCard = identityCard
        };
        var errors = new List<ValidationResult>();
        var isValid = Validator.TryValidateObject(dto, new ValidationContext(dto), errors, true);
        Assert.Equal(expectedValid, isValid);
    }

    [Fact]
    public async Task UpdatePatient_ValidRequest_UpdatesEntityAndContacts()
    {
        using var db = SchemaFixture.Create();
        var patient = new Patient
        {
            PatientId = 1,
            PatientCode = "BN00001",
            FullName = "Nguyen Van Cu",
            Phone = "0901111111",
            Address = "123 Đường Cũ",
            IdentityCard = "123456789012"
        };
        db.Patients.Add(patient);
        await db.SaveChangesAsync();

        var service = new PatientService(db);
        var request = new UpdatePatientRequest
        {
            FullName = "Nguyen Van Moi",
            Phone = "0902222222",
            Gender = "Nam",
            DateOfBirth = new DateOnly(1990, 1, 1),
            Address = "456 Đường Mới",
            Ward = "Phường Bến Nghé",
            Province = "TP Hồ Chí Minh",
            IdentityCard = "123456789012",
            IdentityCardIssuedDate = new DateOnly(2015, 1, 1),
            Email = "nguyenvanmoi@example.com",
            ContactFullName = "Nguyen Thi Vo",
            ContactRelationship = "Vợ",
            ContactPhone = "0903333333"
        };

        await service.UpdateAsync(1, request, default);

        var updated = await db.Patients.Include(p => p.PatientContacts).SingleAsync(p => p.PatientId == 1);
        Assert.Equal("Nguyen Van Moi", updated.FullName);
        Assert.Equal("0902222222", updated.Phone);
        Assert.Equal("Nam", updated.Gender);
        Assert.Contains("Phường Bến Nghé", updated.Address);
        Assert.Contains("TP Hồ Chí Minh", updated.Address);
        Assert.Single(updated.PatientContacts);
        Assert.Equal("Nguyen Thi Vo", updated.PatientContacts.First().FullName);
        Assert.Equal("0903333333", updated.PatientContacts.First().Phone);
    }

    [Fact]
    public async Task UpdatePatient_WhenNotFound_ThrowsNotFoundException()
    {
        using var db = SchemaFixture.Create();
        var service = new PatientService(db);
        var request = new UpdatePatientRequest
        {
            FullName = "Nguyen Van A",
            Phone = "0901234567"
        };

        await Assert.ThrowsAsync<NotFoundException>(() => service.UpdateAsync(999, request, default));
    }

    [Fact]
    public async Task UpdatePatient_FutureBirthDate_ThrowsBadRequestException()
    {
        using var db = SchemaFixture.Create();
        var patient = new Patient { PatientId = 1, FullName = "Test", Phone = "0901234567" };
        db.Patients.Add(patient);
        await db.SaveChangesAsync();

        var service = new PatientService(db);
        var request = new UpdatePatientRequest
        {
            FullName = "Test",
            Phone = "0901234567",
            DateOfBirth = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(10))
        };

        await Assert.ThrowsAsync<BadRequestException>(() => service.UpdateAsync(1, request, default));
    }

    [Fact]
    public async Task UpdatePatient_DuplicateIdentityCard_ThrowsBadRequestException()
    {
        using var db = SchemaFixture.Create();
        db.Patients.AddRange(
            new Patient { PatientId = 1, FullName = "BN 1", Phone = "0901111111", IdentityCard = "123456789012" },
            new Patient { PatientId = 2, FullName = "BN 2", Phone = "0902222222", IdentityCard = "987654321098" }
        );
        await db.SaveChangesAsync();

        var service = new PatientService(db);
        var request = new UpdatePatientRequest
        {
            FullName = "BN 2 Updated",
            Phone = "0902222222",
            IdentityCard = "123456789012" // Trùng với BN 1
        };

        var ex = await Assert.ThrowsAsync<BadRequestException>(() => service.UpdateAsync(2, request, default));
        Assert.Contains("đã được sử dụng bởi bệnh nhân khác", ex.Message);
    }

    [Fact]
    public async Task UpdatePatient_IdentityCardIssuedUnder14YearsOld_ThrowsBadRequestException()
    {
        using var db = SchemaFixture.Create();
        var patient = new Patient { PatientId = 1, FullName = "Test", Phone = "0901234567" };
        db.Patients.Add(patient);
        await db.SaveChangesAsync();

        var service = new PatientService(db);
        var request = new UpdatePatientRequest
        {
            FullName = "Test",
            Phone = "0901234567",
            DateOfBirth = new DateOnly(2010, 1, 1),
            IdentityCard = "123456789012",
            IdentityCardIssuedDate = new DateOnly(2020, 1, 1) // Lúc này mới 10 tuổi
        };

        var ex = await Assert.ThrowsAsync<BadRequestException>(() => service.UpdateAsync(1, request, default));
        Assert.Contains("14 tuổi", ex.Message);
    }
}

using COSGeniusSystem.Data;
using COSGeniusSystem.DTOs.Patients;
using COSGeniusSystem.Exceptions;
using COSGeniusSystem.Models;
using COSGeniusSystem.Services;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace COSGeniusSystem.UnitTests.Patients;

public class PatientServiceTests
{
    private static ApplicationDbContext CreateInMemoryDbContext()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(databaseName: $"ClinicHerbDb_Patient_{Guid.NewGuid()}")
            .Options;

        return new ApplicationDbContext(options);
    }

    [Fact]
    public async Task SearchPatientsAsync_WithKeyword_ReturnsMatchingPatients()
    {
        // Arrange
        using var context = CreateInMemoryDbContext();
        context.Patients.AddRange(
            new Patient { PatientId = 1, PatientCode = "BN00001", FullName = "Nguyen Van A", Phone = "0901234567" },
            new Patient { PatientId = 2, PatientCode = "BN00002", FullName = "Tran Thi B", Phone = "0909999888" },
            new Patient { PatientId = 3, PatientCode = "BN00003", FullName = "Le Van C", Phone = "0912345678" }
        );
        await context.SaveChangesAsync();

        var service = new PatientService(context);

        // Act
        var result = await service.SearchPatientsAsync(new PatientSearchQuery { Keyword = "Nguyen" });

        // Assert
        Assert.Single(result);
        Assert.Equal("Nguyen Van A", result[0].FullName);
    }

    [Fact]
    public async Task GetPatientDetailAsync_WhenPatientExists_ReturnsDetailWithContacts()
    {
        // Arrange
        using var context = CreateInMemoryDbContext();
        var patient = new Patient
        {
            PatientId = 10,
            PatientCode = "BN00010",
            FullName = "Hoang Van D",
            IdentityCard = "123456789012",
            Email = "hoang@example.com",
            PatientContacts = new List<PatientContact>
            {
                new() { ContactId = 1, FullName = "Hoang Thi E", Relationship = "Vợ", Phone = "0988776655" }
            }
        };
        context.Patients.Add(patient);
        await context.SaveChangesAsync();

        var service = new PatientService(context);

        // Act
        var result = await service.GetPatientDetailAsync(10);

        // Assert
        Assert.NotNull(result);
        Assert.Equal("Hoang Van D", result.FullName);
        Assert.Single(result.Contacts);
        Assert.Equal("Hoang Thi E", result.Contacts[0].FullName);
    }

    [Fact]
    public async Task GetPatientDetailAsync_WhenNotExists_ThrowsNotFoundException()
    {
        // Arrange
        using var context = CreateInMemoryDbContext();
        var service = new PatientService(context);

        // Act & Assert
        await Assert.ThrowsAsync<NotFoundException>(() => service.GetPatientDetailAsync(999));
    }

    [Fact]
    public async Task CreatePatientV1Async_ValidRequest_CreatesPatientAndMedicalRecord()
    {
        // Arrange
        using var context = CreateInMemoryDbContext();
        var service = new PatientService(context);

        var request = new CreatePatientRequest
        {
            FullName = "Dang Thi Thu",
            Gender = "Nữ",
            DateOfBirth = new DateOnly(1995, 5, 20),
            Phone = "0977112233",
            Address = "123 Le Loi, Q1",
            ContactFullName = "Dang Van Cha",
            ContactRelationship = "Bố",
            ContactPhone = "0977112244"
        };

        // Act
        var result = await service.CreatePatientV1Async(request);

        // Assert
        Assert.NotNull(result);
        Assert.True(result.PatientId > 0);
        Assert.Equal("Dang Thi Thu", result.FullName);
        Assert.Single(result.Contacts);

        var records = await context.MedicalRecords.Where(r => r.PatientId == result.PatientId).ToListAsync();
        Assert.Single(records);
        Assert.Equal("Đang theo dõi", records[0].Status);
    }

    [Fact]
    public async Task CreatePatientV1Async_FutureBirthDate_ThrowsBadRequestException()
    {
        // Arrange
        using var context = CreateInMemoryDbContext();
        var service = new PatientService(context);

        var request = new CreatePatientRequest
        {
            FullName = "Em Be Tuong Lai",
            DateOfBirth = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(5))
        };

        // Act & Assert
        await Assert.ThrowsAsync<BadRequestException>(() => service.CreatePatientV1Async(request));
    }

    [Fact]
    public async Task GetVisitHistoryAsync_WhenExists_ReturnsExamSheets()
    {
        // Arrange
        using var context = CreateInMemoryDbContext();
        var doctor = new Doctor { DoctorId = 1, FullName = "BS Nguyen Van A" };
        var patient = new Patient { PatientId = 1, FullName = "Benh Nhan Test" };
        var mr = new MedicalRecord { RecordId = 1, Patient = patient, Status = "Đang theo dõi" };
        var es = new ExamSheet
        {
            ExamSheetId = 1,
            Record = mr,
            Doctor = doctor,
            ExamDate = DateTime.Now,
            Symptoms = "Dau lung",
            FinalDiagnosis = "Thoai hoa cot song"
        };
        context.Doctors.Add(doctor);
        context.Patients.Add(patient);
        context.MedicalRecords.Add(mr);
        context.ExamSheets.Add(es);
        await context.SaveChangesAsync();

        var service = new PatientService(context);

        // Act
        var result = await service.GetVisitHistoryAsync(1);

        // Assert
        Assert.Single(result);
        Assert.Equal("KCB00001", result[0].KcbCode);
        Assert.Equal("MR00001", result[0].MrCode);
        Assert.Equal("BS Nguyen Van A", result[0].DoctorName);
    }

    [Fact]
    public async Task GetExamSheetDetailAsync_WhenExists_ReturnsFullDetail()
    {
        // Arrange
        using var context = CreateInMemoryDbContext();
        var doctor = new Doctor { DoctorId = 1, FullName = "BS Nguyen Van A" };
        var patient = new Patient { PatientId = 1, FullName = "Benh Nhan Test" };
        var mr = new MedicalRecord { RecordId = 1, Patient = patient, Status = "Đang theo dõi" };
        var serviceItem = new Service { ServiceId = 1, ServiceCode = "DV01", ServiceName = "Châm cứu", Unit = "Lần", Price = 100000, IsActive = true };
        var herb = new Herb { HerbId = 1, CommonName = "Cam thảo", HerbCode = "CT01", Unit = "g" };

        var es = new ExamSheet
        {
            ExamSheetId = 1,
            Record = mr,
            Doctor = doctor,
            ExamDate = DateTime.Now,
            Symptoms = "Mệt mỏi",
            FinalDiagnosis = "Khí hư"
        };
        var usage = new UsageHistory
        {
            UsageId = 1,
            ExamSheet = es,
            Service = serviceItem,
            ExecutionTime = DateTime.Now,
            Quantity = 1,
            PerformedByNavigation = doctor
        };
        var formula = new RemedyFormula
        {
            FormulaId = 1,
            ExamSheet = es,
            FormulaName = "Bài thuốc bổ khí",
            DosageDays = 7,
            TotalDoses = 7,
            CreatedAt = DateTime.Now
        };
        var detail = new FormulaDetail
        {
            FormulaDetailId = 1,
            Formula = formula,
            Herb = herb,
            DosagePerDose = 10,
            RoleInFormula = "Quân"
        };

        context.Doctors.Add(doctor);
        context.Patients.Add(patient);
        context.MedicalRecords.Add(mr);
        context.Services.Add(serviceItem);
        context.Herbs.Add(herb);
        context.ExamSheets.Add(es);
        context.UsageHistories.Add(usage);
        context.RemedyFormulas.Add(formula);
        context.FormulaDetails.Add(detail);
        await context.SaveChangesAsync();

        var service = new PatientService(context);

        // Act
        var result = await service.GetExamSheetDetailAsync(1);

        // Assert
        Assert.NotNull(result);
        Assert.Equal("KCB00001", result.KcbCode);
        Assert.Single(result.Services);
        Assert.Equal("Châm cứu", result.Services[0].ServiceName);
        Assert.Single(result.Prescriptions);
        Assert.Equal("Cam thảo", result.Prescriptions[0].Details[0].HerbName);
    }
}

using COSGeniusSystem.Data;
using COSGeniusSystem.DTOs.DoctorClinical;
using COSGeniusSystem.Exceptions;
using COSGeniusSystem.Models;
using COSGeniusSystem.Services.Impl;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace COSGeniusSystem.UnitTests.DoctorClinical;

public class DoctorClinicalServiceTests
{
    private static ApplicationDbContext CreateInMemoryDbContext()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(databaseName: $"ClinicHerbDb_DoctorClinical_{Guid.NewGuid()}")
            .Options;

        return new ApplicationDbContext(options);
    }

    private static async Task<(Doctor doctor, Patient patient, MedicalRecord record)> SeedBasicDataAsync(ApplicationDbContext context)
    {
        var doctor = new Doctor
        {
            DoctorId = 1,
            FullName = "BS. CKI Nguyễn Văn Tuấn",
            Specialization = "Y học cổ truyền",
            IsActive = true
        };

        var patient = new Patient
        {
            PatientId = 10,
            PatientCode = "BN00010",
            FullName = "Trần Thị Lan",
            Gender = "Nữ",
            DateOfBirth = new DateOnly(1990, 5, 12),
            Phone = "0987112233",
            Address = "123 Đường Đông Y, TP.HCM",
            AllergyNote = "Dị ứng phấn hoa",
            CreatedAt = DateTime.Now
        };

        var contact = new PatientContact
        {
            ContactId = 1,
            PatientId = 10,
            FullName = "Trần Văn Nam",
            Relationship = "Chồng",
            Phone = "0909123456"
        };
        patient.PatientContacts.Add(contact);

        var record = new MedicalRecord
        {
            RecordId = 100,
            PatientId = 10,
            DoctorId = 1,
            ExamDate = DateTime.Now,
            Status = "Chờ khám",
            Symptoms = "[Ưu tiên] Đau mỏi vai gáy",
            DoctorAdvice = new ClinicalMetadataJson { Status = "Chờ khám", IsPriority = true }.ToJson(),
            Doctor = doctor,
            Patient = patient
        };

        context.Doctors.Add(doctor);
        context.Patients.Add(patient);
        context.MedicalRecords.Add(record);

        await context.SaveChangesAsync();

        return (doctor, patient, record);
    }

    [Fact]
    public async Task GetExamQueueAsync_ReturnsCorrectQueueAndCounters()
    {
        // Arrange
        using var context = CreateInMemoryDbContext();
        await SeedBasicDataAsync(context);

        var service = new DoctorClinicalService(context);

        // Act
        var result = await service.GetExamQueueAsync(new DoctorExamFilterDto
        {
            DoctorId = 1,
            FromDate = DateTime.Today,
            ToDate = DateTime.Today
        });

        // Assert
        Assert.NotNull(result);
        Assert.Equal(1, result.TotalCount);
        Assert.Equal(1, result.WaitingCount);
        Assert.Equal(0, result.ExaminingCount);
        Assert.Single(result.Items);
        Assert.True(result.Items[0].IsPriority);
        Assert.Equal("Gọi vào khám", result.Items[0].AllowedAction);
    }

    [Fact]
    public async Task CallPatientAsync_WhenWaiting_ChangesStatusToExamining()
    {
        // Arrange
        using var context = CreateInMemoryDbContext();
        var (_, _, sheet) = await SeedBasicDataAsync(context);

        var service = new DoctorClinicalService(context);

        // Act
        var workspace = await service.CallPatientAsync(sheet.RecordId);

        // Assert
        Assert.NotNull(workspace);
        Assert.Equal("Đang khám", workspace.Status);
        Assert.False(workspace.IsReadOnly);

        var updatedSheet = await context.MedicalRecords.FindAsync(sheet.RecordId);
        Assert.NotNull(updatedSheet);
        Assert.Equal("IN_PROGRESS", updatedSheet.Status);
    }

    [Fact]
    public async Task CallPatientAsync_WhenCompleted_ThrowsBadRequestException()
    {
        // Arrange
        using var context = CreateInMemoryDbContext();
        var (_, _, sheet) = await SeedBasicDataAsync(context);

        sheet.DoctorAdvice = new ClinicalMetadataJson { Status = "Hoàn tất" }.ToJson();
        sheet.Status = "COMPLETED";
        await context.SaveChangesAsync();

        var service = new DoctorClinicalService(context);

        // Act & Assert
        await Assert.ThrowsAsync<BadRequestException>(() => service.CallPatientAsync(sheet.RecordId));
    }

    [Fact]
    public async Task SaveClinicalExamAsync_SavesVitalSignsAndTraditionalMedicine()
    {
        // Arrange
        using var context = CreateInMemoryDbContext();
        var (_, _, sheet) = await SeedBasicDataAsync(context);

        var service = new DoctorClinicalService(context);

        var request = new SaveClinicalExamRequestDto
        {
            VitalSigns = new VitalSignsDto
            {
                PulseRate = 78,
                Temperature = 36.8m,
                RespiratoryRate = 18,
                Height = 160,
                Weight = 52,
                SpO2 = 98
            },
            Symptoms = "Đau mỏi vai gáy 3 ngày",
            PreliminaryDiagnosis = "Hội chứng cổ vai cánh tay",
            TraditionalMedicine = new TraditionalFourExamsDto
            {
                VongChan = "Sắc mặt hồng hào, lưỡi nhạt rêu trắng mỏng",
                ThietChan = "Mạch trầm tế",
                TraditionalDiagnosis = "Khí huyết ứ trệ kinh lạc"
            },
            IcdCode = "M54.2",
            IcdName = "Cervicalgia"
        };

        // Act
        var workspace = await service.SaveClinicalExamAsync(sheet.RecordId, request);

        // Assert
        Assert.NotNull(workspace);
        Assert.Equal("Đang khám", workspace.Status);
        Assert.Equal(78, workspace.ClinicalExam.VitalSigns.PulseRate);
        Assert.Equal("Khí huyết ứ trệ kinh lạc", workspace.ClinicalExam.TraditionalMedicine.TraditionalDiagnosis);
        Assert.Equal("M54.2", workspace.ClinicalExam.IcdCode);
    }

    [Fact]
    public async Task CompleteExaminationAsync_MarksCompletedAndReadOnly()
    {
        // Arrange
        using var context = CreateInMemoryDbContext();
        var (_, _, sheet) = await SeedBasicDataAsync(context);

        var service = new DoctorClinicalService(context);

        // Act
        var workspace = await service.CompleteExaminationAsync(sheet.RecordId, null);

        // Assert
        Assert.NotNull(workspace);
        Assert.Equal("Hoàn tất", workspace.Status);
        Assert.True(workspace.IsReadOnly);

        var updatedSheet = await context.MedicalRecords.FindAsync(sheet.RecordId);
        Assert.NotNull(updatedSheet);
        Assert.Equal("COMPLETED", updatedSheet.Status);
    }

    [Fact]
    public async Task SaveServiceIndicationAsync_SavesProtocolAndUsageHistory()
    {
        // Arrange
        using var context = CreateInMemoryDbContext();
        var (_, _, sheet) = await SeedBasicDataAsync(context);

        var svc = new Service
        {
            ServiceId = 1,
            ServiceCode = "DV001",
            ServiceName = "Châm cứu trị liệu",
            Price = 150000,
            Unit = "Lần",
            IsActive = true
        };
        context.Services.Add(svc);
        await context.SaveChangesAsync();

        var service = new DoctorClinicalService(context);

        var request = new SaveServiceIndicationRequestDto
        {
            TreatmentType = "Theo liệu trình",
            TotalSessions = 5,
            IndicationNote = "Châm cứu kết hợp xoa bóp",
            Services = new List<SaveServiceItemDto>
            {
                new() { ServiceId = 1, Note = "Châm cứu huyệt phong trì, đại chùy" }
            }
        };

        // Act
        var result = await service.SaveServiceIndicationAsync(sheet.RecordId, request);

        // Assert
        Assert.NotNull(result);
        Assert.Equal("Theo liệu trình", result.TreatmentType);
        Assert.Equal(5, result.TotalSessions);
        Assert.Single(result.Services);
        Assert.Equal("Châm cứu trị liệu", result.Services[0].ServiceName);
    }

    [Fact]
    public async Task GetPrescriptionCatalogAsync_ReturnsFormulasAndHerbs()
    {
        // Arrange
        using var context = CreateInMemoryDbContext();
        var herb = new Herb
        {
            HerbId = 1,
            HerbCode = "DL001",
            CommonName = "Cam thảo",
            Unit = "g",
            StandardPrice = 500,
            MinThreshold = 10,
            IsActive = true
        };
        context.Herbs.Add(herb);

        var formula = new HerbalFormula
        {
            HerbalFormulaId = 1,
            FormulaName = "Bát trân thang",
            UsageInstructions = "Sắc uống ngày 1 thang",
            Status = "APPROVED",
            IsActive = true,
            CreatedAt = DateTime.Now
        };
        formula.HerbalFormulaDetails.Add(new HerbalFormulaDetail
        {
            HerbalFormulaDetailId = 1,
            HerbId = 1,
            DosagePerDose = 10,
            RoleInFormula = "Quân",
            Herb = herb
        });
        context.HerbalFormulas.Add(formula);
        await context.SaveChangesAsync();

        var service = new DoctorClinicalService(context);

        // Act
        var catalog = await service.GetPrescriptionCatalogAsync();

        // Assert
        Assert.NotNull(catalog);
        Assert.Single(catalog.Herbs);
        Assert.Single(catalog.FormulaTemplates);
        Assert.Equal("Bát trân thang", catalog.FormulaTemplates[0].FormulaName);
    }

    [Fact]
    public async Task SavePrescriptionAsync_SaveDraft_CreatesRemedyFormula()
    {
        // Arrange
        using var context = CreateInMemoryDbContext();
        var (_, _, sheet) = await SeedBasicDataAsync(context);

        var herb = new Herb
        {
            HerbId = 1,
            HerbCode = "DL001",
            CommonName = "Cam thảo",
            Unit = "g",
            StandardPrice = 500,
            MinThreshold = 10,
            IsActive = true
        };
        context.Herbs.Add(herb);
        await context.SaveChangesAsync();

        var service = new DoctorClinicalService(context);

        var request = new SavePrescriptionRequestDto
        {
            ActionType = "SAVE_DRAFT",
            PrescriptionMethod = "Tự kê đơn",
            TotalDoses = 5,
            PrescriptionNote = "Uống sau ăn 30 phút",
            Ingredients = new List<SavePrescriptionIngredientDto>
            {
                new() { HerbId = 1, GramsPerDose = 12, Note = "Vị thuốc chính" }
            }
        };

        // Act
        var prescription = await service.SavePrescriptionAsync(sheet.RecordId, request);

        // Assert
        Assert.NotNull(prescription);
        Assert.Equal("Nháp", prescription.Status);
        Assert.False(prescription.IsLocked);
        Assert.Single(prescription.Ingredients);
        Assert.Equal(60, prescription.Ingredients[0].TotalGrams); // 12g * 5 thang
    }

    [Fact]
    public async Task SavePrescriptionAsync_WhenSentToHerbalist_LocksPrescriptionAndPreventsFurtherEdits()
    {
        // Arrange
        using var context = CreateInMemoryDbContext();
        var (_, _, sheet) = await SeedBasicDataAsync(context);

        var herb = new Herb
        {
            HerbId = 1,
            HerbCode = "DL001",
            CommonName = "Cam thảo",
            Unit = "g",
            StandardPrice = 500,
            MinThreshold = 10,
            IsActive = true
        };
        context.Herbs.Add(herb);
        await context.SaveChangesAsync();

        var service = new DoctorClinicalService(context);

        var request = new SavePrescriptionRequestDto
        {
            ActionType = "SEND_HERBALIST",
            TotalDoses = 3,
            Ingredients = new List<SavePrescriptionIngredientDto>
            {
                new() { HerbId = 1, GramsPerDose = 10 }
            }
        };

        // Act: Send to Herbalist
        var sentPrescription = await service.SavePrescriptionAsync(sheet.RecordId, request);

        // Assert: Locked
        Assert.Equal("Đã gửi Herbalist", sentPrescription.Status);
        Assert.True(sentPrescription.IsLocked);

        // Act & Assert: Trying to edit after sending throws BadRequestException
        await Assert.ThrowsAsync<BadRequestException>(() => service.SavePrescriptionAsync(sheet.RecordId, request));
    }
}

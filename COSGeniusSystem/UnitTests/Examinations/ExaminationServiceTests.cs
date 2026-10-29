using COSGeniusSystem.Data;
using COSGeniusSystem.DTOs.Examinations;
using COSGeniusSystem.Exceptions;
using COSGeniusSystem.Models;
using COSGeniusSystem.Services.Examinations;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace COSGeniusSystem.UnitTests.Examinations;

public class ExaminationServiceTests
{
    private static ApplicationDbContext CreateInMemoryDbContext()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(databaseName: $"ClinicHerbDb_Exam_{Guid.NewGuid()}")
            .Options;

        return new ApplicationDbContext(options);
    }

    [Fact]
    public async Task GetVisitsAsync_FiltersByDoctor_ReturnsMatchingVisits()
    {
        // Arrange
        using var context = CreateInMemoryDbContext();
        var doc1 = new Doctor { DoctorId = 1, FullName = "Bác sĩ A", IsActive = true };
        var doc2 = new Doctor { DoctorId = 2, FullName = "Bác sĩ B", IsActive = true };
        var patient1 = new Patient { PatientId = 1, FullName = "Bệnh nhân 1", Phone = "0901" };
        var patient2 = new Patient { PatientId = 2, FullName = "Bệnh nhân 2", Phone = "0902" };
        var mr1 = new MedicalRecord { RecordId = 1, Patient = patient1, Status = "Đang theo dõi" };
        var mr2 = new MedicalRecord { RecordId = 2, Patient = patient2, Status = "Đang theo dõi" };

        context.Doctors.AddRange(doc1, doc2);
        context.Patients.AddRange(patient1, patient2);
        context.MedicalRecords.AddRange(mr1, mr2);
        context.ExamSheets.AddRange(
            new ExamSheet { ExamSheetId = 1, Record = mr1, Doctor = doc1, ExamDate = DateTime.Now, FinalDiagnosis = "Chờ khám" },
            new ExamSheet { ExamSheetId = 2, Record = mr2, Doctor = doc2, ExamDate = DateTime.Now, FinalDiagnosis = "Chờ khám" }
        );
        await context.SaveChangesAsync();

        var service = new ExaminationService(context);

        // Act
        var result = await service.GetVisitsAsync(new ExaminationFilterQuery { DoctorId = 1 });

        // Assert
        Assert.Single(result);
        Assert.Equal("KCB00001", result[0].KcbCode);
        Assert.Equal("Bác sĩ A", result[0].DoctorName);
    }

    [Fact]
    public async Task CreateVisitAsync_WhenPatientHasNoActiveRecord_CreatesRecordAndExamSheet()
    {
        // Arrange
        using var context = CreateInMemoryDbContext();
        var doctor = new Doctor { DoctorId = 1, FullName = "BS Chuyên Khoa", IsActive = true };
        var patient = new Patient { PatientId = 1, FullName = "Nguyen Van Dang Ky" };
        context.Doctors.Add(doctor);
        context.Patients.Add(patient);
        await context.SaveChangesAsync();

        var service = new ExaminationService(context);
        var request = new CreateExaminationVisitRequest
        {
            PatientId = 1,
            DoctorId = 1,
            Symptoms = "Đau đầu dữ dội",
            IsPriority = true
        };

        // Act
        var result = await service.CreateVisitAsync(request);

        // Assert
        Assert.NotNull(result);
        Assert.True(result.ExamSheetId > 0);
        Assert.Equal("KCB00001", result.KcbCode);
        Assert.True(result.IsPriority);
        Assert.Equal("Chờ khám", result.Status);

        var createdSheet = await context.ExamSheets.FindAsync(result.ExamSheetId);
        Assert.NotNull(createdSheet);
        Assert.Contains("[Ưu tiên]", createdSheet.Symptoms);

        var records = await context.MedicalRecords.Where(r => r.PatientId == 1).ToListAsync();
        Assert.Single(records);
    }

    [Fact]
    public async Task CancelVisitAsync_WhenStatusIsChoKham_DeletesExamSheet()
    {
        // Arrange
        using var context = CreateInMemoryDbContext();
        var doctor = new Doctor { DoctorId = 1, FullName = "BS A", IsActive = true };
        var patient = new Patient { PatientId = 1, FullName = "BN A" };
        var mr = new MedicalRecord { RecordId = 1, Patient = patient, Status = "Đang theo dõi" };
        var sheet = new ExamSheet
        {
            ExamSheetId = 10,
            Record = mr,
            Doctor = doctor,
            ExamDate = DateTime.Now,
            FinalDiagnosis = "Chờ khám"
        };
        context.Doctors.Add(doctor);
        context.Patients.Add(patient);
        context.MedicalRecords.Add(mr);
        context.ExamSheets.Add(sheet);
        await context.SaveChangesAsync();

        var service = new ExaminationService(context);

        // Act
        await service.CancelVisitAsync(10);

        // Assert
        var deletedSheet = await context.ExamSheets.FindAsync(10);
        Assert.Null(deletedSheet);
    }

    [Fact]
    public async Task CancelVisitAsync_WhenAlreadyCompleted_ThrowsBadRequestException()
    {
        // Arrange
        using var context = CreateInMemoryDbContext();
        var doctor = new Doctor { DoctorId = 1, FullName = "BS A", IsActive = true };
        var patient = new Patient { PatientId = 1, FullName = "BN A" };
        var mr = new MedicalRecord { RecordId = 1, Patient = patient, Status = "Đang theo dõi" };
        var sheet = new ExamSheet
        {
            ExamSheetId = 11,
            Record = mr,
            Doctor = doctor,
            ExamDate = DateTime.Now,
            FinalDiagnosis = "Đã khám"
        };
        context.Doctors.Add(doctor);
        context.Patients.Add(patient);
        context.MedicalRecords.Add(mr);
        context.ExamSheets.Add(sheet);
        await context.SaveChangesAsync();

        var service = new ExaminationService(context);

        // Act & Assert
        await Assert.ThrowsAsync<BadRequestException>(() => service.CancelVisitAsync(11));
    }
}


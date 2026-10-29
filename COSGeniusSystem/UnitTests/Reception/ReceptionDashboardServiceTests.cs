using COSGeniusSystem.Data;
using COSGeniusSystem.Models;
using COSGeniusSystem.Services.Reception;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace COSGeniusSystem.UnitTests.Reception;

public class ReceptionDashboardServiceTests
{
    private static ApplicationDbContext CreateInMemoryDbContext()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(databaseName: $"ClinicHerbDb_Dashboard_{Guid.NewGuid()}")
            .Options;

        return new ApplicationDbContext(options);
    }

    [Fact]
    public async Task GetTodayStatsAsync_CalculatesCorrectStats()
    {
        // Arrange
        using var context = CreateInMemoryDbContext();
        var doctor = new Doctor { DoctorId = 1, FullName = "Bác sĩ A", IsActive = true };
        var serviceItem = new Service { ServiceId = 1, ServiceCode = "DV01", ServiceName = "Châm cứu", Unit = "Lần", Price = 100000, IsActive = true };
        var patient = new Patient { PatientId = 1, FullName = "Bệnh nhân A" };
        var mr = new MedicalRecord { RecordId = 1, Patient = patient, Status = "Đang theo dõi" };

        var waitingExam = new ExamSheet
        {
            ExamSheetId = 1,
            Record = mr,
            Doctor = doctor,
            ExamDate = DateTime.Now,
            FinalDiagnosis = "Chờ khám"
        };
        var completedExam = new ExamSheet
        {
            ExamSheetId = 2,
            Record = mr,
            Doctor = doctor,
            ExamDate = DateTime.Now,
            FinalDiagnosis = "Viêm khớp"
        };

        var waitingUsage = new UsageHistory
        {
            UsageId = 1,
            ExamSheet = waitingExam,
            Service = serviceItem,
            ExecutionTime = DateTime.Now,
            Quantity = 1,
            ResultNote = null // Chờ điều trị
        };
        var completedUsage = new UsageHistory
        {
            UsageId = 2,
            ExamSheet = completedExam,
            Service = serviceItem,
            ExecutionTime = DateTime.Now,
            Quantity = 1,
            ResultNote = "Đã thực hiện xong"
        };

        context.Doctors.Add(doctor);
        context.Services.Add(serviceItem);
        context.Patients.Add(patient);
        context.MedicalRecords.Add(mr);
        context.ExamSheets.AddRange(waitingExam, completedExam);
        context.UsageHistories.AddRange(waitingUsage, completedUsage);
        await context.SaveChangesAsync();

        var service = new ReceptionDashboardService(context);

        // Act
        var result = await service.GetTodayStatsAsync();

        // Assert
        Assert.NotNull(result);
        Assert.Equal(4, result.TodayTotalVisits); // 2 exams + 2 treatments
        Assert.Equal(1, result.WaitingExaminationCount);
        Assert.Equal(1, result.WaitingTreatmentCount);
        Assert.Equal(2, result.CompletedVisitsCount); // 1 completed exam + 1 completed treatment
        Assert.Equal(2, result.NewExaminationCount);
        Assert.Equal(2, result.TreatmentCount);
    }

    [Fact]
    public async Task GetQuickQueuesAsync_ReturnsTopWaitingQueues()
    {
        // Arrange
        using var context = CreateInMemoryDbContext();
        var doctor = new Doctor { DoctorId = 1, FullName = "Bác sĩ B", IsActive = true };
        var serviceItem = new Service { ServiceId = 1, ServiceCode = "DV01", ServiceName = "Xoa bóp", Unit = "Lần", Price = 120000, IsActive = true };
        var patient1 = new Patient { PatientId = 1, PatientCode = "BN00001", FullName = "Bệnh nhân 1" };
        var patient2 = new Patient { PatientId = 2, PatientCode = "BN00002", FullName = "Bệnh nhân Ưu Tiên" };
        var mr1 = new MedicalRecord { RecordId = 1, Patient = patient1, Status = "Đang theo dõi" };
        var mr2 = new MedicalRecord { RecordId = 2, Patient = patient2, Status = "Đang theo dõi" };

        var examNormal = new ExamSheet
        {
            ExamSheetId = 1,
            Record = mr1,
            Doctor = doctor,
            ExamDate = DateTime.Now.AddMinutes(-10),
            FinalDiagnosis = "Chờ khám",
            Symptoms = "Đau lưng"
        };
        var examPriority = new ExamSheet
        {
            ExamSheetId = 2,
            Record = mr2,
            Doctor = doctor,
            ExamDate = DateTime.Now,
            FinalDiagnosis = "Chờ khám",
            Symptoms = "[Ưu tiên] Cấp cứu đau đầu"
        };

        var usage = new UsageHistory
        {
            UsageId = 1,
            ExamSheet = examNormal,
            Service = serviceItem,
            ExecutionTime = DateTime.Now,
            Quantity = 1,
            ResultNote = "Chờ điều trị"
        };

        context.Doctors.Add(doctor);
        context.Services.Add(serviceItem);
        context.Patients.AddRange(patient1, patient2);
        context.MedicalRecords.AddRange(mr1, mr2);
        context.ExamSheets.AddRange(examNormal, examPriority);
        context.UsageHistories.Add(usage);
        await context.SaveChangesAsync();

        var service = new ReceptionDashboardService(context);

        // Act
        var result = await service.GetQuickQueuesAsync(top: 2);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(2, result.WaitingExaminations.Count);
        // Ưu tiên lên đầu
        Assert.True(result.WaitingExaminations[0].IsPriority);
        Assert.Equal("Bệnh nhân Ưu Tiên", result.WaitingExaminations[0].FullName);

        Assert.Single(result.WaitingTreatments);
        Assert.Equal("YCDT00001", result.WaitingTreatments[0].YcdtCode);
        Assert.Equal("Bệnh nhân 1", result.WaitingTreatments[0].FullName);
    }
}


using COSGeniusSystem.Data;
using COSGeniusSystem.DTOs.Treatments;
using COSGeniusSystem.Exceptions;
using COSGeniusSystem.Models;
using COSGeniusSystem.Services.Treatments;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace COSGeniusSystem.UnitTests.Treatments;

public class TreatmentProtocolServiceTests
{
    private static ApplicationDbContext CreateInMemoryDbContext()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(databaseName: $"ClinicHerbDb_Treatment_{Guid.NewGuid()}")
            .Options;

        return new ApplicationDbContext(options);
    }

    [Fact]
    public async Task GetQueueAsync_PendingTreatmentsToday_ReturnsQueueWithSessionInfo()
    {
        // Arrange
        using var context = CreateInMemoryDbContext();
        var doc = new Doctor { DoctorId = 1, FullName = "Bác sĩ Trị Liệu", IsActive = true };
        var serviceItem = new Service { ServiceId = 1, ServiceCode = "DV01", ServiceName = "Xoa bóp bấm huyệt", Unit = "Lần", Price = 150000, IsActive = true };
        var patient = new Patient { PatientId = 1, PatientCode = "BN00001", FullName = "Người Bệnh", Phone = "0987654321" };
        var mr = new MedicalRecord { RecordId = 1, Patient = patient, Status = "Đang theo dõi" };
        var protocol = new TreatmentProtocol
        {
            ProtocolId = 1,
            Record = mr,
            Doctor = doc,
            ProtocolName = "Phác đồ phục hồi đau lưng",
            StartDate = DateOnly.FromDateTime(DateTime.Today),
            Description = "Liệu trình 10 buổi trị liệu"
        };
        var es = new ExamSheet
        {
            ExamSheetId = 1,
            Record = mr,
            Doctor = doc,
            ExamDate = DateTime.Now,
            FinalDiagnosis = "Chỉ định trị liệu"
        };
        var usage1 = new UsageHistory
        {
            UsageId = 1,
            ExamSheet = es,
            Service = serviceItem,
            ExecutionTime = DateTime.Now.AddHours(-1),
            Quantity = 1,
            ResultNote = "Đã hoàn thành tốt"
        };
        var usage2 = new UsageHistory
        {
            UsageId = 2,
            ExamSheet = es,
            Service = serviceItem,
            ExecutionTime = DateTime.Now,
            Quantity = 1,
            ResultNote = null // Đang chờ điều trị
        };

        context.Doctors.Add(doc);
        context.Services.Add(serviceItem);
        context.Patients.Add(patient);
        context.MedicalRecords.Add(mr);
        context.TreatmentProtocols.Add(protocol);
        context.ExamSheets.Add(es);
        context.UsageHistories.AddRange(usage1, usage2);
        await context.SaveChangesAsync();

        var service = new TreatmentProtocolService(context);

        // Act
        var result = await service.GetQueueAsync(new TreatmentQueueQuery { Date = DateTime.Today });

        // Assert
        Assert.Single(result);
        Assert.Equal("YCDT00002", result[0].YcdtCode);
        Assert.Equal("Người Bệnh", result[0].PatientName);
        Assert.Equal("Buổi 02 / 10", result[0].SessionInfo);
        Assert.Equal("Chờ điều trị", result[0].Status);
    }

    [Fact]
    public async Task CreateTreatmentRequestAsync_ValidRequest_CreatesUsageHistoryAndReturnsDto()
    {
        // Arrange
        using var context = CreateInMemoryDbContext();
        var doc = new Doctor { DoctorId = 2, FullName = "Bác sĩ Trị Liệu 2", IsActive = true };
        var serviceItem = new Service { ServiceId = 5, ServiceCode = "DV05", ServiceName = "Cứu ngải", Unit = "Lần", Price = 100000, IsActive = true };
        var patient = new Patient { PatientId = 5, PatientCode = "BN00005", FullName = "Bệnh nhân Tiếp Nhận" };
        var mr = new MedicalRecord { RecordId = 5, Patient = patient, Status = "Đang theo dõi" };
        var es = new ExamSheet
        {
            ExamSheetId = 5,
            Record = mr,
            Doctor = doc,
            ExamDate = DateTime.Now,
            FinalDiagnosis = "Chỉ định điều trị"
        };

        context.Doctors.Add(doc);
        context.Services.Add(serviceItem);
        context.Patients.Add(patient);
        context.MedicalRecords.Add(mr);
        context.ExamSheets.Add(es);
        await context.SaveChangesAsync();

        var service = new TreatmentProtocolService(context);
        var request = new CreateTreatmentRequest
        {
            PatientId = 5,
            ServiceId = 5,
            DoctorId = 2,
            ExamSheetId = 5,
            Note = "Bệnh nhân mỏi vai gáy"
        };

        // Act
        var result = await service.CreateTreatmentRequestAsync(request);

        // Assert
        Assert.NotNull(result);
        Assert.True(result.UsageId > 0);
        Assert.Equal("YCDT00001", result.YcdtCode);
        Assert.Equal("Bệnh nhân Tiếp Nhận", result.PatientName);
        Assert.Equal("Chờ điều trị", result.Status);

        var savedUsage = await context.UsageHistories.FindAsync(result.UsageId);
        Assert.NotNull(savedUsage);
        Assert.Equal("Bệnh nhân mỏi vai gáy", savedUsage.ResultNote);
    }

    [Fact]
    public async Task CreateTreatmentRequestAsync_InvalidService_ThrowsNotFoundException()
    {
        // Arrange
        using var context = CreateInMemoryDbContext();
        var doc = new Doctor { DoctorId = 1, FullName = "Bác sĩ A", IsActive = true };
        var patient = new Patient { PatientId = 1, FullName = "Bệnh nhân A" };
        context.Doctors.Add(doc);
        context.Patients.Add(patient);
        await context.SaveChangesAsync();

        var service = new TreatmentProtocolService(context);
        var request = new CreateTreatmentRequest
        {
            PatientId = 1,
            ServiceId = 999, // Không tồn tại
            DoctorId = 1
        };

        // Act & Assert
        await Assert.ThrowsAsync<NotFoundException>(() => service.CreateTreatmentRequestAsync(request));
    }
}

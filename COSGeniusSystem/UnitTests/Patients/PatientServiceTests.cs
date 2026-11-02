using COSGeniusSystem.Services;
using CreatePatientRequest = COSGeniusSystem.DTOs.Compatibility.V1.CreatePatientRequest;
using UpdatePatientRequest = COSGeniusSystem.DTOs.Compatibility.V1.UpdatePatientRequest;
using ContactUpdateItem = COSGeniusSystem.DTOs.Compatibility.V1.ContactUpdateItem;
using COSGeniusSystem.Data;
using COSGeniusSystem.DTOs;
using COSGeniusSystem.Exceptions;
using COSGeniusSystem.Models;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace COSGeniusSystem.UnitTests;

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
    public async Task CreatePatientV1Async_ValidRequest_DoesNotCreateUnregisteredMedicalRecord()
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
        Assert.Empty(records);
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
    public async Task History_ReadsMedicalRecordsAndPrescriptionSnapshots()
    {
        using var db = SchemaFixture.Create();
        var record = SchemaFixture.Record();
        db.MedicalRecords.Add(record);
        var herb = new Herb { HerbId = 1, HerbCode = "H1", CommonName = "Renamed herb", Unit = "g" };
        db.HerbalPrescriptions.Add(new HerbalPrescription
        {
            PrescriptionId = 1, Record = record, PrescriptionName = "Prescription", Status = "SENT",
            CreatedByNavigation = record.Doctor.User!, TotalDoses = 7, CreatedAt = DateTime.UtcNow,
            HerbalPrescriptionDetails = [new HerbalPrescriptionDetail
            {
                Herb = herb, HerbNameSnapshot = "Original herb", DosagePerDose = 10
            }]
        });
        await db.SaveChangesAsync();
        var service = new PatientService(db);
        var history = Assert.Single(await service.GetVisitHistoryAsync(record.PatientId));
        Assert.Equal(record.RecordId, history.RecordId);
        Assert.Equal("COMPLETED", history.Status);
        var detail = await service.GetMedicalRecordDetailAsync(record.RecordId);
        Assert.Equal("Original herb", Assert.Single(Assert.Single(detail.Prescriptions).Details).HerbName);
    }

    [Fact]
    public async Task TreatmentHistory_IncludesPlannedSessionsWithoutUsage()
    {
        using var db = SchemaFixture.Create();
        var record = SchemaFixture.Record();
        var plan = SchemaFixture.Plan(record);
        db.TreatmentPlans.Add(plan);
        db.TreatmentSessions.AddRange(
            new TreatmentSession { SessionId = 1, Plan = plan, SessionNumber = 1, Status = "COMPLETED" },
            new TreatmentSession { SessionId = 2, Plan = plan, SessionNumber = 2, Status = "PLANNED" });
        await db.SaveChangesAsync();
        var service = new PatientService(db);
        var summary = Assert.Single(await service.GetTreatmentProtocolsAsync(record.PatientId));
        Assert.Equal(1, summary.CompletedSessions);
        Assert.Equal(3, summary.TotalSessions);
        var sessions = await service.GetTreatmentSessionsAsync(plan.PlanId);
        Assert.Equal(2, sessions.Count);
        Assert.Equal("PLANNED", sessions[1].Status);
        Assert.Null(sessions[1].ExecutionTime);
    }
}

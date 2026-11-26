using System;
using System.Linq;
using System.Threading.Tasks;
using COSGeniusSystem.Data;
using COSGeniusSystem.DTOs;
using COSGeniusSystem.Exceptions;
using COSGeniusSystem.Models;
using COSGeniusSystem.Services;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace COSGeniusSystem.UnitTests;

public class PatientServiceTests
{
    private static ApplicationDbContext NewDb() => new(
        new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase($"PatientCompact_{Guid.NewGuid():N}").Options);

    private static DateOnly Today => DateOnly.FromDateTime(DateTime.UtcNow.AddHours(7));

    private static async Task SeedPatients(ApplicationDbContext db)
    {
        db.Patients.AddRange(
            new Patient { PatientId = 1, PatientCode = "BN001", FullName = "Nguyen Van A", Phone = "0912345678" },
            new Patient { PatientId = 2, PatientCode = "BN002", FullName = "Tran Thi B", Phone = "0987654321" },
            new Patient { PatientId = 3, PatientCode = "BN003", FullName = "Le Van C", Phone = "0933333333" });
        await db.SaveChangesAsync();
    }

    private static CreatePatientRequest CreateRequest(string phone = "0912345678") => new()
        { FullName = "Patient A", Phone = phone };

    private static UpdatePatientRequest UpdateRequest(string phone = "0912345678") => new()
        { FullName = "Patient Updated", Phone = phone };

    private static async Task AssertPatientUnchanged(ApplicationDbContext db, int id,
        string name, string phone)
    {
        // Reload from the store, not from tracked values after a failed update.
        db.ChangeTracker.Clear();
        var saved = await db.Patients.AsNoTracking().SingleAsync(x => x.PatientId == id);
        Assert.Equal(name, saved.FullName);
        Assert.Equal(phone, saved.Phone);
    }
    #region CheckDuplicatePhoneAsync

    [Fact]
    public async Task CheckDuplicatePhoneAsync_EquivalentPhone_ReturnsMatch()
    {
        using var db = NewDb();
        await SeedPatients(db);
        var result = await new PatientService(db).CheckDuplicatePhoneAsync("+84912345678", null, default);
        Assert.Equal(1, Assert.Single(result).PatientId);
    }

    [Fact]
    public async Task CheckDuplicatePhoneAsync_ExcludeSelf_ReturnsNoMatch()
    {
        using var db = NewDb();
        await SeedPatients(db);
        var result = await new PatientService(db).CheckDuplicatePhoneAsync("0912345678", 1, default);
        Assert.Empty(result);
    }

    [Fact]
    public async Task CheckDuplicatePhoneAsync_NoMatch_ReturnsEmpty()
    {
        using var db = NewDb();
        await SeedPatients(db);
        var result = await new PatientService(db).CheckDuplicatePhoneAsync("0944444444", null, default);
        Assert.Empty(result);
    }

    #endregion

    #region GetAllAsync

    [Fact]
    public async Task GetAllAsync_ExistingPatients_ReturnsRequestedPage()
    {
        using var db = NewDb();
        await SeedPatients(db);
        var result = await new PatientService(db).GetAllAsync(2, 2, default);
        Assert.Equal(3, result.TotalCount);
        Assert.Equal(3, Assert.Single(result.Items).PatientId);
    }

    [Fact]
    public async Task GetAllAsync_EmptyDatabase_ReturnsEmptyPage()
    {
        using var db = NewDb();
        var result = await new PatientService(db).GetAllAsync(1, 10, default);
        Assert.Empty(result.Items);
        Assert.Equal(0, result.TotalCount);
    }

    [Fact]
    public async Task GetAllAsync_InvalidPage_ThrowsBadRequest()
    {
        using var db = NewDb();
        await Assert.ThrowsAsync<BadRequestException>(() => new PatientService(db).GetAllAsync(0, 10, default));
    }

    #endregion

    #region SearchAsync

    [Fact]
    public async Task SearchAsync_Keyword_ReturnsMatchingPatient()
    {
        using var db = NewDb();
        await SeedPatients(db);
        var result = await new PatientService(db).SearchAsync("Nguyen", 1, 10, default);
        Assert.Equal(1, Assert.Single(result.Items).PatientId);
        Assert.Equal(1, result.TotalCount);
    }

    [Fact]
    public async Task SearchAsync_InvalidPageSize_ThrowsBadRequest()
    {
        using var db = NewDb();
        await Assert.ThrowsAsync<BadRequestException>(() => new PatientService(db).SearchAsync(null, 1, 101, default));
    }

    [Fact]
    public async Task SearchAsync_UnknownDoctorUser_ReturnsEmpty()
    {
        using var db = NewDb();
        await SeedPatients(db);
        var result = await new PatientService(db).SearchAsync(null, 1, 10, default, doctorUserId: 999);
        Assert.Empty(result.Items);
        Assert.Equal(0, result.TotalCount);
    }

    #endregion

    #region GetByIdAsync

    [Fact]
    public async Task GetByIdAsync_ExistingPatient_ReturnsCorrectPatient()
    {
        using var db = NewDb();
        await SeedPatients(db);
        var result = await new PatientService(db).GetByIdAsync(2, default);
        Assert.Equal(2, result.PatientId);
        Assert.Equal("Tran Thi B", result.FullName);
        Assert.Equal("0987654321", result.Phone);
    }

    [Fact]
    public async Task GetByIdAsync_UnknownPatient_ThrowsNotFound()
    {
        using var db = NewDb();
        await Assert.ThrowsAsync<NotFoundException>(() => new PatientService(db).GetByIdAsync(999, default));
    }

    [Fact]
    public async Task GetByIdAsync_InvalidIdentifier_ThrowsNotFound()
    {
        using var db = NewDb();
        await Assert.ThrowsAsync<NotFoundException>(() => new PatientService(db).GetByIdAsync(0, default));
    }

    #endregion

    #region CreateAsync

    [Fact]
    public async Task CreateAsync_ValidRequest_CreatesPatientAndContact()
    {
        using var db = NewDb();
        var request = CreateRequest("+84912345678");
        request.ContactFullName = "Contact A";
        request.ContactRelationship = "Parent";
        request.ContactPhone = "0987654321";
        var result = await new PatientService(db).CreateAsync(request, default);
        db.ChangeTracker.Clear();
        var saved = Assert.Single(await db.Patients.Include(x => x.PatientContacts).ToListAsync());
        Assert.Equal(result.PatientId, saved.PatientId);
        Assert.Equal("Patient A", saved.FullName);
        Assert.Equal("0912345678", saved.Phone);
        Assert.Equal("Contact A", Assert.Single(saved.PatientContacts).FullName);
        Assert.Empty(await db.MedicalRecords.ToListAsync());
    }

    [Fact]
    public async Task CreateAsync_MissingPhone_ThrowsBadRequestWithoutSaving()
    {
        using var db = NewDb();
        var request = CreateRequest("");
        await Assert.ThrowsAsync<BadRequestException>(() => new PatientService(db).CreateAsync(request, default));
        Assert.Empty(await db.Patients.ToListAsync());
    }

    [Fact]
    public async Task CreateAsync_FutureBirthDate_ThrowsBadRequestWithoutSaving()
    {
        using var db = NewDb();
        var request = CreateRequest();
        request.DateOfBirth = Today.AddDays(1);
        await Assert.ThrowsAsync<BadRequestException>(() => new PatientService(db).CreateAsync(request, default));
        Assert.Empty(await db.Patients.ToListAsync());
    }

    [Fact]
    public async Task CreateAsync_DuplicateUnconfirmed_RequiresConfirmation()
    {
        using var db = NewDb();
        await SeedPatients(db);
        await Assert.ThrowsAsync<DuplicatePatientPhoneException>(() => new PatientService(db)
            .CreateAsync(CreateRequest("+84912345678"), default));
        Assert.Equal(3, await db.Patients.CountAsync());
    }

    [Fact]
    public async Task CreateAsync_DuplicateConfirmed_CreatesSeparatePatient()
    {
        using var db = NewDb();
        await SeedPatients(db);
        var request = CreateRequest("+84912345678");
        request.ConfirmDuplicatePhone = true;
        var result = await new PatientService(db).CreateAsync(request, default);
        Assert.NotEqual(1, result.PatientId);
        db.ChangeTracker.Clear();
        Assert.Equal(4, await db.Patients.CountAsync());
        Assert.Equal(2, await db.Patients.CountAsync(x => x.Phone == "0912345678"));
    }

    #endregion

    #region UpdateAsync

    [Fact]
    public async Task UpdateAsync_ValidRequest_UpdatesPatientAndAddsContact()
    {
        using var db = NewDb();
        await SeedPatients(db);
        var request = UpdateRequest("+84912345678");
        request.Gender = "Nam";
        request.DateOfBirth = new DateOnly(1990, 1, 1);
        request.Address = "456 Đường Mới";
        request.Ward = "Phường Bến Nghé";
        request.Province = "TP Hồ Chí Minh";
        request.IdentityCard = "123456789012";
        request.IdentityCardIssuedDate = new DateOnly(2015, 1, 1);
        request.Email = "patient.updated@example.com";
        request.ContactFullName = "Contact A";
        request.ContactRelationship = "Parent";
        request.ContactPhone = "0944444444";
        await new PatientService(db).UpdateAsync(1, request, default);
        db.ChangeTracker.Clear();
        var saved = await db.Patients.Include(x => x.PatientContacts).SingleAsync(x => x.PatientId == 1);
        Assert.Equal("Patient Updated", saved.FullName);
        Assert.Equal("0912345678", saved.Phone);
        Assert.Equal("Nam", saved.Gender);
        Assert.Equal(new DateOnly(1990, 1, 1), saved.DateOfBirth);
        Assert.Equal("123456789012", saved.IdentityCard);
        Assert.Equal("patient.updated@example.com", saved.Email);
        Assert.Contains("Phường Bến Nghé", saved.Address);
        Assert.Contains("TP Hồ Chí Minh", saved.Address);
        var contact = Assert.Single(saved.PatientContacts);
        Assert.Equal("Contact A", contact.FullName);
        Assert.Equal("0944444444", contact.Phone);
        Assert.Equal("Tran Thi B", (await db.Patients.SingleAsync(x => x.PatientId == 2)).FullName);
    }

    [Fact]
    public async Task UpdateAsync_UnknownPatient_ThrowsNotFound()
    {
        using var db = NewDb();
        await Assert.ThrowsAsync<NotFoundException>(() => new PatientService(db).UpdateAsync(999, UpdateRequest(), default));
        Assert.Empty(await db.Patients.ToListAsync());
    }

    [Fact]
    public async Task UpdateAsync_MissingPhone_RejectsAndPreservesPatient()
    {
        using var db = NewDb();
        await SeedPatients(db);
        await Assert.ThrowsAsync<BadRequestException>(() => new PatientService(db).UpdateAsync(1, UpdateRequest(""), default));
        await AssertPatientUnchanged(db, 1, "Nguyen Van A", "0912345678");
    }

    [Fact]
    public async Task UpdateAsync_DuplicateUnconfirmed_RejectsAndPreservesPatient()
    {
        using var db = NewDb();
        await SeedPatients(db);
        await Assert.ThrowsAsync<DuplicatePatientPhoneException>(() => new PatientService(db)
            .UpdateAsync(2, UpdateRequest("0912345678"), default));
        await AssertPatientUnchanged(db, 2, "Tran Thi B", "0987654321");
    }

    [Fact]
    public async Task UpdateAsync_DuplicateConfirmed_UpdatesOnlySelectedPatient()
    {
        using var db = NewDb();
        await SeedPatients(db);
        var request = UpdateRequest("0912345678");
        request.ConfirmDuplicatePhone = true;
        await new PatientService(db).UpdateAsync(2, request, default);
        await AssertPatientUnchanged(db, 1, "Nguyen Van A", "0912345678");
        var changed = await db.Patients.AsNoTracking().SingleAsync(x => x.PatientId == 2);
        Assert.Equal("Patient Updated", changed.FullName);
        Assert.Equal("0912345678", changed.Phone);
        Assert.Equal(3, await db.Patients.CountAsync());
    }

    #endregion

    #region DeleteAsync

    [Fact]
    public async Task DeleteAsync_UnreferencedPatient_DeletesSelectedPatient()
    {
        using var db = NewDb();
        await SeedPatients(db);
        await new PatientService(db).DeleteAsync(1, default);
        db.ChangeTracker.Clear();
        Assert.False(await db.Patients.AnyAsync(x => x.PatientId == 1));
        Assert.Equal(2, await db.Patients.CountAsync());
    }

    [Fact]
    public async Task DeleteAsync_UnknownPatient_ThrowsNotFound()
    {
        using var db = NewDb();
        await Assert.ThrowsAsync<NotFoundException>(() => new PatientService(db).DeleteAsync(999, default));
    }

    [Fact]
    public async Task DeleteAsync_PatientWithContact_ThrowsConflictAndPreservesData()
    {
        using var db = NewDb();
        var patient = new Patient { PatientId = 1, FullName = "A", Phone = "0912345678" };
        patient.PatientContacts.Add(new PatientContact { FullName = "Contact A", Phone = "0987654321" });
        db.Patients.Add(patient);
        await db.SaveChangesAsync();
        await Assert.ThrowsAsync<ConflictException>(() => new PatientService(db).DeleteAsync(1, default));
        db.ChangeTracker.Clear();
        Assert.Single(await db.Patients.ToListAsync());
        Assert.Single(await db.PatientContacts.ToListAsync());
    }

    #endregion

    #region SearchPatientsAsync

    [Fact]
    public async Task SearchPatientsAsync_Keyword_ReturnsMatchingPatient()
    {
        using var db = NewDb();
        await SeedPatients(db);
        var result = await new PatientService(db).SearchPatientsAsync(new PatientSearchQuery { Keyword = "Nguyen" });
        Assert.Equal(1, Assert.Single(result).PatientId);
    }

    [Fact]
    public async Task SearchPatientsAsync_NoMatch_ReturnsEmpty()
    {
        using var db = NewDb();
        await SeedPatients(db);
        var result = await new PatientService(db).SearchPatientsAsync(new PatientSearchQuery { Keyword = "NoMatch" });
        Assert.Empty(result);
    }

    [Fact]
    public async Task SearchPatientsAsync_NoKeyword_ReturnsPatientsInDescendingIdOrder()
    {
        using var db = NewDb();
        await SeedPatients(db);
        var result = await new PatientService(db).SearchPatientsAsync(new PatientSearchQuery());
        Assert.Equal(new[] { 3, 2, 1 }, result.Select(x => x.PatientId).ToArray());
    }

    #endregion

    #region GetPatientDetailAsync

    [Fact]
    public async Task GetPatientDetailAsync_WithContacts_ReturnsPatientAndContacts()
    {
        using var db = NewDb();
        var patient = new Patient { PatientId = 1, FullName = "A", Phone = "0912345678" };
        patient.PatientContacts.Add(new PatientContact { FullName = "Contact A", Phone = "0987654321" });
        db.Patients.Add(patient);
        await db.SaveChangesAsync();
        var result = await new PatientService(db).GetPatientDetailAsync(1);
        Assert.Equal(1, result.PatientId);
        Assert.Equal("A", result.FullName);
        Assert.Equal("Contact A", Assert.Single(result.Contacts).FullName);
    }

    [Fact]
    public async Task GetPatientDetailAsync_WithoutContacts_ReturnsEmptyContacts()
    {
        using var db = NewDb();
        await SeedPatients(db);
        var result = await new PatientService(db).GetPatientDetailAsync(1);
        Assert.Equal("Nguyen Van A", result.FullName);
        Assert.Empty(result.Contacts);
    }

    [Fact]
    public async Task GetPatientDetailAsync_UnknownPatient_ThrowsNotFound()
    {
        using var db = NewDb();
        await Assert.ThrowsAsync<NotFoundException>(() => new PatientService(db).GetPatientDetailAsync(999));
    }

    #endregion

    #region GetVisitHistoryAsync

    [Fact]
    public async Task GetVisitHistoryAsync_WithRecord_ReturnsPatientsVisit()
    {
        using var db = SchemaFixture.Create();
        var record = SchemaFixture.Record();
        db.MedicalRecords.Add(record);
        await db.SaveChangesAsync();
        var result = await new PatientService(db).GetVisitHistoryAsync(record.PatientId);
        Assert.Equal(record.RecordId, Assert.Single(result).RecordId);
    }

    [Fact]
    public async Task GetVisitHistoryAsync_WithoutRecord_ReturnsEmpty()
    {
        using var db = NewDb();
        await SeedPatients(db);
        Assert.Empty(await new PatientService(db).GetVisitHistoryAsync(1));
    }

    [Fact]
    public async Task GetVisitHistoryAsync_UnknownPatient_ThrowsNotFound()
    {
        using var db = NewDb();
        await Assert.ThrowsAsync<NotFoundException>(() => new PatientService(db).GetVisitHistoryAsync(999));
    }

    #endregion

    #region GetMedicalRecordDetailAsync

    [Fact]
    public async Task GetMedicalRecordDetailAsync_UsesPrescriptionHerbSnapshot()
    {
        using var db = SchemaFixture.Create();
        var record = SchemaFixture.Record();
        db.MedicalRecords.Add(record);
        var herb = new Herb { HerbId = 1, HerbCode = "H1", CommonName = "Renamed herb", Unit = "g" };
        db.HerbalPrescriptions.Add(new HerbalPrescription
        {
            PrescriptionId = 1, Record = record, PrescriptionName = "Prescription", Status = "SENT",
            CreatedByNavigation = record.Doctor.User!, TotalDoses = 1, CreatedAt = DateTime.UtcNow,
            HerbalPrescriptionDetails = [new HerbalPrescriptionDetail
                { Herb = herb, HerbNameSnapshot = "Original herb", DosagePerDose = 10 }]
        });
        await db.SaveChangesAsync();
        var result = await new PatientService(db).GetMedicalRecordDetailAsync(record.RecordId);
        Assert.Equal("Original herb", Assert.Single(Assert.Single(result.Prescriptions).Details).HerbName);
    }

    [Fact]
    public async Task GetMedicalRecordDetailAsync_WithoutIndications_ReturnsEmptyLists()
    {
        using var db = SchemaFixture.Create();
        var record = SchemaFixture.Record();
        db.MedicalRecords.Add(record);
        await db.SaveChangesAsync();
        var result = await new PatientService(db).GetMedicalRecordDetailAsync(record.RecordId);
        Assert.Equal(record.RecordId, result.RecordId);
        Assert.Empty(result.Prescriptions);
        Assert.Empty(result.Services);
    }

    [Fact]
    public async Task GetMedicalRecordDetailAsync_UnknownRecord_ThrowsNotFound()
    {
        using var db = NewDb();
        await Assert.ThrowsAsync<NotFoundException>(() => new PatientService(db).GetMedicalRecordDetailAsync(999));
    }

    #endregion

    #region GetTreatmentProtocolsAsync

    [Fact]
    public async Task GetTreatmentProtocolsAsync_CountsOnlyCompletedSessions()
    {
        using var db = SchemaFixture.Create();
        var record = SchemaFixture.Record();
        var plan = SchemaFixture.Plan(record);
        db.TreatmentSessions.AddRange(
            new TreatmentSession { SessionId = 1, Plan = plan, SessionNumber = 1, Status = "COMPLETED" },
            new TreatmentSession { SessionId = 2, Plan = plan, SessionNumber = 2, Status = "PLANNED" });
        await db.SaveChangesAsync();
        var result = Assert.Single(await new PatientService(db).GetTreatmentProtocolsAsync(record.PatientId));
        Assert.Equal(plan.PlanId, result.PlanId);
        Assert.Equal(1, result.CompletedSessions);
        Assert.Equal(plan.PlannedSessionCount, result.TotalSessions);
    }

    [Fact]
    public async Task GetTreatmentProtocolsAsync_WithoutPlan_ReturnsEmpty()
    {
        using var db = NewDb();
        await SeedPatients(db);
        Assert.Empty(await new PatientService(db).GetTreatmentProtocolsAsync(1));
    }

    [Fact]
    public async Task GetTreatmentProtocolsAsync_UnknownPatient_ThrowsNotFound()
    {
        using var db = NewDb();
        await Assert.ThrowsAsync<NotFoundException>(() => new PatientService(db).GetTreatmentProtocolsAsync(999));
    }

    #endregion

    #region GetTreatmentSessionsAsync

    [Fact]
    public async Task GetTreatmentSessionsAsync_ExistingPlan_ReturnsOrderedSessions()
    {
        using var db = SchemaFixture.Create();
        var plan = SchemaFixture.Plan(SchemaFixture.Record());
        db.TreatmentSessions.AddRange(
            new TreatmentSession { SessionId = 2, Plan = plan, SessionNumber = 2, Status = "PLANNED" },
            new TreatmentSession { SessionId = 1, Plan = plan, SessionNumber = 1, Status = "COMPLETED" });
        await db.SaveChangesAsync();
        var result = await new PatientService(db).GetTreatmentSessionsAsync(plan.PlanId);
        Assert.Equal(new[] { 1, 2 }, result.Select(x => x.SessionId).ToArray());
        Assert.Equal("PLANNED", result[1].Status);
        Assert.Null(result[1].ExecutionTime);
    }

    [Fact]
    public async Task GetTreatmentSessionsAsync_WithoutSessions_ReturnsEmpty()
    {
        using var db = SchemaFixture.Create();
        var plan = SchemaFixture.Plan(SchemaFixture.Record());
        db.TreatmentPlans.Add(plan);
        await db.SaveChangesAsync();
        Assert.Empty(await new PatientService(db).GetTreatmentSessionsAsync(plan.PlanId));
    }

    [Fact]
    public async Task GetTreatmentSessionsAsync_UnknownPlan_ThrowsNotFound()
    {
        using var db = NewDb();
        await Assert.ThrowsAsync<NotFoundException>(() => new PatientService(db).GetTreatmentSessionsAsync(999));
    }

    #endregion
    #region AdditionalPatientValidation

    [Fact]
    public async Task UpdateAsync_FutureBirthDate_ThrowsBadRequestAndPreservesPatient()
    {
        using var db = NewDb();
        var patient = new Patient { PatientId = 1, FullName = "Test", Phone = "0901234567" };
        db.Patients.Add(patient);
        await db.SaveChangesAsync();

        var service = new PatientService(db);
        var request = new UpdatePatientRequest
        {
            FullName = "Test",
            Phone = "0901234567",
            DateOfBirth = Today.AddDays(1)
        };

        await Assert.ThrowsAsync<BadRequestException>(() => service.UpdateAsync(1, request, default));
        await AssertPatientUnchanged(db, 1, "Test", "0901234567");
    }

    [Fact]
    public async Task UpdateAsync_DuplicateIdentityCard_ThrowsBadRequestAndPreservesPatient()
    {
        using var db = NewDb();
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
        Assert.Contains("already used by another patient", ex.Message);
        await AssertPatientUnchanged(db, 2, "BN 2", "0902222222");
        var saved = await db.Patients.AsNoTracking().SingleAsync(x => x.PatientId == 2);
        Assert.Equal("987654321098", saved.IdentityCard);
    }

    [Fact]
    public async Task UpdateAsync_IdentityCardIssuedUnder14YearsOld_ThrowsBadRequestAndPreservesPatient()
    {
        using var db = NewDb();
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
        Assert.Contains("14 years old", ex.Message);
        await AssertPatientUnchanged(db, 1, "Test", "0901234567");
    }

    #endregion
}


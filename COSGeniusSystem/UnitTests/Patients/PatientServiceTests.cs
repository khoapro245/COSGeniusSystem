using COSGeniusSystem.Data;
using COSGeniusSystem.DTOs;
using COSGeniusSystem.Exceptions;
using COSGeniusSystem.Models;
using COSGeniusSystem.Services;
using Microsoft.EntityFrameworkCore;

namespace COSGeniusSystem.UnitTests;

public class PatientServiceTests
{
    private static ApplicationDbContext NewDb() => new(
        new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase($"PatientMainFunctions_{Guid.NewGuid():N}").Options);

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
        db.ChangeTracker.Clear();
        var saved = await db.Patients.AsNoTracking().SingleAsync(x => x.PatientId == id);
        Assert.Equal(name, saved.FullName);
        Assert.Equal(phone, saved.Phone);
    }
    #region CreateAsync

    [Fact]
    [Trait("Module", "Patient")]
    [Trait("Method", "CreateAsync")]
    [Trait("TestCaseId", "UTC-PAT-MAIN-001")]
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
    [Trait("Module", "Patient")]
    [Trait("Method", "CreateAsync")]
    [Trait("TestCaseId", "UTC-PAT-MAIN-002")]
    public async Task CreateAsync_MissingPhone_ThrowsBadRequestWithoutSaving()
    {
        using var db = NewDb();
        var request = CreateRequest("");
        await Assert.ThrowsAsync<BadRequestException>(() => new PatientService(db).CreateAsync(request, default));
        Assert.Empty(await db.Patients.ToListAsync());
    }

    [Fact]
    [Trait("Module", "Patient")]
    [Trait("Method", "CreateAsync")]
    [Trait("TestCaseId", "UTC-PAT-MAIN-003")]
    public async Task CreateAsync_FutureBirthDate_ThrowsBadRequestWithoutSaving()
    {
        using var db = NewDb();
        var request = CreateRequest();
        request.DateOfBirth = Today.AddDays(1);
        await Assert.ThrowsAsync<BadRequestException>(() => new PatientService(db).CreateAsync(request, default));
        Assert.Empty(await db.Patients.ToListAsync());
    }

    [Fact]
    [Trait("Module", "Patient")]
    [Trait("Method", "CreateAsync")]
    [Trait("TestCaseId", "UTC-PAT-MAIN-004")]
    public async Task CreateAsync_DuplicateUnconfirmed_RequiresConfirmation()
    {
        using var db = NewDb();
        await SeedPatients(db);
        await Assert.ThrowsAsync<DuplicatePatientPhoneException>(() => new PatientService(db)
            .CreateAsync(CreateRequest("+84912345678"), default));
        Assert.Equal(3, await db.Patients.CountAsync());
    }

    [Fact]
    [Trait("Module", "Patient")]
    [Trait("Method", "CreateAsync")]
    [Trait("TestCaseId", "UTC-PAT-MAIN-005")]
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
    [Trait("Module", "Patient")]
    [Trait("Method", "UpdateAsync")]
    [Trait("TestCaseId", "UTC-PAT-MAIN-006")]
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
    [Trait("Module", "Patient")]
    [Trait("Method", "UpdateAsync")]
    [Trait("TestCaseId", "UTC-PAT-MAIN-007")]
    public async Task UpdateAsync_UnknownPatient_ThrowsNotFound()
    {
        using var db = NewDb();
        await Assert.ThrowsAsync<NotFoundException>(() => new PatientService(db).UpdateAsync(999, UpdateRequest(), default));
        Assert.Empty(await db.Patients.ToListAsync());
    }

    [Fact]
    [Trait("Module", "Patient")]
    [Trait("Method", "UpdateAsync")]
    [Trait("TestCaseId", "UTC-PAT-MAIN-008")]
    public async Task UpdateAsync_MissingPhone_RejectsAndPreservesPatient()
    {
        using var db = NewDb();
        await SeedPatients(db);
        await Assert.ThrowsAsync<BadRequestException>(() => new PatientService(db).UpdateAsync(1, UpdateRequest(""), default));
        await AssertPatientUnchanged(db, 1, "Nguyen Van A", "0912345678");
    }

    [Fact]
    [Trait("Module", "Patient")]
    [Trait("Method", "UpdateAsync")]
    [Trait("TestCaseId", "UTC-PAT-MAIN-009")]
    public async Task UpdateAsync_DuplicateUnconfirmed_RejectsAndPreservesPatient()
    {
        using var db = NewDb();
        await SeedPatients(db);
        await Assert.ThrowsAsync<DuplicatePatientPhoneException>(() => new PatientService(db)
            .UpdateAsync(2, UpdateRequest("0912345678"), default));
        await AssertPatientUnchanged(db, 2, "Tran Thi B", "0987654321");
    }

    [Fact]
    [Trait("Module", "Patient")]
    [Trait("Method", "UpdateAsync")]
    [Trait("TestCaseId", "UTC-PAT-MAIN-010")]
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

}

using COSGeniusSystem.Commons;
using COSGeniusSystem.Data;
using COSGeniusSystem.DTOs;
using COSGeniusSystem.Exceptions;
using COSGeniusSystem.Models;
using COSGeniusSystem.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using System.Security.Claims;

namespace COSGeniusSystem.UnitTests;

/// <summary>
/// 15 representative service tests: CreateAsync (5), UpdateAsync (5), CancelAsync (5).
/// Uses an isolated EF InMemory store per test. Does not prove SQL transactions,
/// real rowversion generation, concurrent queue allocation, or date-change raw SQL.
/// Does not depend on SchemaFixture or other test classes.
/// </summary>
public sealed class ExaminationRegistrationTests
{
    private const int ReceptionUserId = 100;
    private const int RegistrationId = 101;

    private static byte[] Version() => new byte[] { 1, 2, 3, 4, 5, 6, 7, 8 };

    private static byte[] StaleVersion() => new byte[] { 9, 2, 3, 4, 5, 6, 7, 8 };

    private static ClaimsPrincipal ReceptionActor() => new(new ClaimsIdentity(new[]
    {
        new Claim(ClaimTypes.NameIdentifier, ReceptionUserId.ToString()),
        new Claim(ClaimTypes.Role, RoleConstants.ReceptionStaff)
    }, "test"));

    private sealed class Scope : IDisposable
    {
        public ApplicationDbContext Db { get; }
        public ExaminationService Service { get; }
        public DateOnly BusinessDate { get; }

        private Scope()
        {
            BusinessDate = DateOnly.FromDateTime(DateTime.UtcNow.AddHours(7)).AddDays(2);
            Db = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseInMemoryDatabase($"RegistrationMain_{Guid.NewGuid():N}")
                .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
                .Options);

            // Only initialize new rows. Never advance versions to simulate SQL concurrency.
            Db.SavingChanges += (_, _) =>
            {
                Db.ChangeTracker.DetectChanges();
                foreach (var entry in Db.ChangeTracker.Entries().Where(e => e.State == EntityState.Added))
                {
                    var property = entry.Metadata.FindProperty("RowVersion");
                    if (property?.ClrType == typeof(byte[])
                        && (entry.Property("RowVersion").CurrentValue is not byte[] bytes || bytes.Length == 0))
                        entry.Property("RowVersion").CurrentValue = Version();
                }
            };

            Service = new ExaminationService(Db, new PatientService(Db));
        }

        public static async Task<Scope> CreateAsync()
        {
            var scope = new Scope();
            var doctorRole = new Role { RoleId = 1, RoleCode = RoleConstants.Doctor, RoleName = "Doctor" };
            var receptionist = new User
            {
                UserId = ReceptionUserId,
                Username = "reception.test",
                FullName = "Reception Test",
                PasswordHash = "test-only",
                IsActive = true
            };
            var doctorUser1 = new User
            {
                UserId = 1,
                Username = "doctor.one",
                FullName = "Doctor One",
                PasswordHash = "test-only",
                IsActive = true
            };
            var doctorUser2 = new User
            {
                UserId = 2,
                Username = "doctor.two",
                FullName = "Doctor Two",
                PasswordHash = "test-only",
                IsActive = true
            };
            doctorUser1.UserRoles.Add(new UserRole { Role = doctorRole });
            doctorUser2.UserRoles.Add(new UserRole { Role = doctorRole });
            scope.Db.Users.AddRange(receptionist, doctorUser1, doctorUser2);
            scope.Db.Doctors.AddRange(
                new Doctor
                {
                    DoctorId = 1,
                    FullName = "Doctor One",
                    User = doctorUser1,
                    LicenseNumber = "TEST-DOC-1",
                    IsActive = true
                },
                new Doctor
                {
                    DoctorId = 2,
                    FullName = "Doctor Two",
                    User = doctorUser2,
                    LicenseNumber = "TEST-DOC-2",
                    IsActive = true
                });
            scope.Db.Patients.Add(new Patient
            {
                PatientId = 1,
                PatientCode = "BN001",
                FullName = "Patient One",
                Phone = "0912345678"
            });
            await scope.Db.SaveChangesAsync();
            return scope;
        }

        public CreateRegistrationRequest CreateRequest(int patientId = 1, int doctorId = 1) => new()
        {
            PatientId = patientId,
            DoctorId = doctorId,
            BusinessDate = BusinessDate,
            Session = "AFTERNOON",
            IsPriority = true,
            Symptoms = "Shoulder pain",
            Note = "New visit"
        };

        public UpdateRegistrationRequest UpdateRequest(byte[]? version = null) => new()
        {
            DoctorId = 2,
            BusinessDate = BusinessDate,
            Session = "AFTERNOON",
            IsPriority = true,
            Symptoms = "Updated symptoms",
            Note = "Updated note",
            RowVersion = version ?? Version()
        };

        public static CancelTreatment CancelRequest(string reason = "Patient requested cancellation", byte[]? version = null) => new()
        {
            Reason = reason,
            RowVersion = version ?? Version()
        };

        public async Task AddRegistrationAsync(string status = "WAITING")
        {
            Db.ExaminationRegistrations.Add(new ExaminationRegistration
            {
                RegistrationId = ExaminationRegistrationTests.RegistrationId,
                RegistrationCode = "KCB00101",
                PatientId = 1,
                DoctorId = 1,
                RegisteredBy = ReceptionUserId,
                RegisteredAt = DateTime.UtcNow,
                BusinessDate = BusinessDate,
                Session = "AFTERNOON",
                QueueNumber = 1,
                Status = status,
                RowVersion = Version(),
                Note = "Original note",
                Symptoms = "Original symptoms",
                IsPriority = false,
                StartedAt = status == "IN_PROGRESS" ? DateTime.UtcNow : null
            });
            Db.ExaminationQueueCounters.Add(new ExaminationQueueCounter
            {
                BusinessDate = BusinessDate,
                LastNumber = 1
            });
            await Db.SaveChangesAsync();
        }

        public async Task<ExaminationRegistration> ReloadAsync()
        {
            Db.ChangeTracker.Clear();
            return await Db.ExaminationRegistrations.AsNoTracking()
                .SingleAsync(r => r.RegistrationId == ExaminationRegistrationTests.RegistrationId);
        }

        public async Task AssertUnchangedAsync(string status = "WAITING")
        {
            var saved = await ReloadAsync();
            Assert.Equal(status, saved.Status);
            Assert.Equal(1, saved.PatientId);
            Assert.Equal(1, saved.DoctorId);
            Assert.Equal(BusinessDate, saved.BusinessDate);
            Assert.Equal("AFTERNOON", saved.Session);
            Assert.Equal(1, saved.QueueNumber);
            Assert.Equal("Original note", saved.Note);
            Assert.Equal("Original symptoms", saved.Symptoms);
            Assert.False(saved.IsPriority);
            Assert.Null(saved.CancelledAt);
            Assert.Equal(Version(), saved.RowVersion);
            Assert.Equal(1, await Db.ExaminationRegistrations.CountAsync());
            Assert.Equal(1, (await Db.ExaminationQueueCounters.AsNoTracking().SingleAsync()).LastNumber);
        }

        public void Dispose() => Db.Dispose();
    }

    #region ExaminationService.CreateAsync

    [Fact]
    [Trait("Module", "ExaminationRegistration")]
    [Trait("Method", "CreateAsync")]
    [Trait("TestCaseId", "UTC-REG-MAIN-001")]
    public async Task CreateAsync_ValidRequest_CreatesWaitingRegistrationAndQueueNumber()
    {
        using var s = await Scope.CreateAsync();
        s.Db.ExaminationQueueCounters.Add(new ExaminationQueueCounter
        {
            BusinessDate = s.BusinessDate,
            LastNumber = 7
        });
        await s.Db.SaveChangesAsync();

        var result = await s.Service.CreateAsync(ReceptionActor(), s.CreateRequest(), default);

        s.Db.ChangeTracker.Clear();
        var saved = Assert.Single(await s.Db.ExaminationRegistrations.AsNoTracking().ToListAsync());
        Assert.Equal(saved.RegistrationId, result.RegistrationId);
        Assert.Equal("WAITING", result.Status);
        Assert.Equal("WAITING", saved.Status);
        Assert.Equal(1, saved.PatientId);
        Assert.Equal(1, saved.DoctorId);
        Assert.Equal(ReceptionUserId, saved.RegisteredBy);
        Assert.Equal(s.BusinessDate, saved.BusinessDate);
        Assert.Equal("AFTERNOON", saved.Session);
        Assert.Equal(8, saved.QueueNumber);
        Assert.True(saved.IsPriority);
        Assert.Equal("Shoulder pain", saved.Symptoms);
        Assert.Equal("New visit", saved.Note);
        Assert.Equal(8, (await s.Db.ExaminationQueueCounters.SingleAsync()).LastNumber);
        Assert.Empty(await s.Db.MedicalRecords.ToListAsync());
    }

    [Fact]
    [Trait("Module", "ExaminationRegistration")]
    [Trait("Method", "CreateAsync")]
    [Trait("TestCaseId", "UTC-REG-MAIN-002")]
    public async Task CreateAsync_UnknownPatient_ThrowsNotFoundWithoutCreatingRegistration()
    {
        using var s = await Scope.CreateAsync();
        await Assert.ThrowsAsync<NotFoundException>(() =>
            s.Service.CreateAsync(ReceptionActor(), s.CreateRequest(patientId: 999), default));
        s.Db.ChangeTracker.Clear();
        Assert.Empty(await s.Db.ExaminationRegistrations.ToListAsync());
        Assert.Empty(await s.Db.ExaminationQueueCounters.ToListAsync());
    }

    [Fact]
    [Trait("Module", "ExaminationRegistration")]
    [Trait("Method", "CreateAsync")]
    [Trait("TestCaseId", "UTC-REG-MAIN-003")]
    public async Task CreateAsync_InactiveDoctor_ThrowsBadRequestWithoutCreatingRegistration()
    {
        using var s = await Scope.CreateAsync();
        var doctor = await s.Db.Doctors.SingleAsync(d => d.DoctorId == 1);
        doctor.IsActive = false;
        await s.Db.SaveChangesAsync();
        await Assert.ThrowsAsync<BadRequestException>(() =>
            s.Service.CreateAsync(ReceptionActor(), s.CreateRequest(), default));
        s.Db.ChangeTracker.Clear();
        Assert.Empty(await s.Db.ExaminationRegistrations.ToListAsync());
        Assert.Empty(await s.Db.ExaminationQueueCounters.ToListAsync());
    }

    [Fact]
    [Trait("Module", "ExaminationRegistration")]
    [Trait("Method", "CreateAsync")]
    [Trait("TestCaseId", "UTC-REG-MAIN-004")]
    public async Task CreateAsync_InvalidSession_ThrowsBadRequestWithoutCreatingRegistration()
    {
        using var s = await Scope.CreateAsync();
        var request = s.CreateRequest();
        request.Session = "EVENING";
        await Assert.ThrowsAsync<BadRequestException>(() =>
            s.Service.CreateAsync(ReceptionActor(), request, default));
        s.Db.ChangeTracker.Clear();
        Assert.Empty(await s.Db.ExaminationRegistrations.ToListAsync());
        Assert.Empty(await s.Db.ExaminationQueueCounters.ToListAsync());
    }

    [Fact]
    [Trait("Module", "ExaminationRegistration")]
    [Trait("Method", "CreateAsync")]
    [Trait("TestCaseId", "UTC-REG-MAIN-005")]
    public async Task CreateAsync_PatientHasWaitingRegistration_ThrowsConflictWithoutDuplicateOrQueueAdvance()
    {
        using var s = await Scope.CreateAsync();
        await s.AddRegistrationAsync();
        await Assert.ThrowsAsync<ConflictException>(() =>
            s.Service.CreateAsync(ReceptionActor(), s.CreateRequest(), default));
        await s.AssertUnchangedAsync();
    }

    #endregion

    #region ExaminationService.UpdateAsync

    [Fact]
    [Trait("Module", "ExaminationRegistration")]
    [Trait("Method", "UpdateAsync")]
    [Trait("TestCaseId", "UTC-REG-MAIN-006")]
    public async Task UpdateAsync_ValidWaitingRegistration_UpdatesAssignmentAndDetailsWithinSameDate()
    {
        using var s = await Scope.CreateAsync();
        await s.AddRegistrationAsync();
        var result = await s.Service.UpdateAsync(ReceptionActor(), RegistrationId, s.UpdateRequest(), default);
        var saved = await s.ReloadAsync();
        Assert.Equal(RegistrationId, result.RegistrationId);
        Assert.Equal(2, result.DoctorId);
        Assert.Equal(2, saved.DoctorId);
        Assert.Equal("Updated note", saved.Note);
        Assert.Equal("Updated symptoms", saved.Symptoms);
        Assert.True(saved.IsPriority);
        Assert.NotNull(saved.UpdatedAt);
        Assert.Equal("WAITING", saved.Status);
        Assert.Equal(1, saved.PatientId);
        Assert.Equal(ReceptionUserId, saved.RegisteredBy);
        Assert.Equal(s.BusinessDate, saved.BusinessDate);
        Assert.Equal(1, saved.QueueNumber);
        Assert.Equal(1, await s.Db.ExaminationRegistrations.CountAsync());
        Assert.Equal(1, (await s.Db.ExaminationQueueCounters.AsNoTracking().SingleAsync()).LastNumber);
        Assert.Empty(await s.Db.MedicalRecords.ToListAsync());
    }

    [Fact]
    [Trait("Module", "ExaminationRegistration")]
    [Trait("Method", "UpdateAsync")]
    [Trait("TestCaseId", "UTC-REG-MAIN-007")]
    public async Task UpdateAsync_UnknownRegistration_ThrowsNotFoundWithoutCreatingRegistration()
    {
        using var s = await Scope.CreateAsync();
        await Assert.ThrowsAsync<NotFoundException>(() =>
            s.Service.UpdateAsync(ReceptionActor(), 999, s.UpdateRequest(), default));
        s.Db.ChangeTracker.Clear();
        Assert.Empty(await s.Db.ExaminationRegistrations.ToListAsync());
        Assert.Empty(await s.Db.ExaminationQueueCounters.ToListAsync());
    }

    [Fact]
    [Trait("Module", "ExaminationRegistration")]
    [Trait("Method", "UpdateAsync")]
    [Trait("TestCaseId", "UTC-REG-MAIN-008")]
    public async Task UpdateAsync_StartedRegistration_ThrowsConflictAndPreservesRegistration()
    {
        using var s = await Scope.CreateAsync();
        await s.AddRegistrationAsync("IN_PROGRESS");
        var startedAt = (await s.Db.ExaminationRegistrations.SingleAsync()).StartedAt;
        await Assert.ThrowsAsync<ConflictException>(() =>
            s.Service.UpdateAsync(ReceptionActor(), RegistrationId, s.UpdateRequest(), default));
        await s.AssertUnchangedAsync("IN_PROGRESS");
        Assert.Equal(startedAt, (await s.ReloadAsync()).StartedAt);
    }

    [Fact]
    [Trait("Module", "ExaminationRegistration")]
    [Trait("Method", "UpdateAsync")]
    [Trait("TestCaseId", "UTC-REG-MAIN-009")]
    public async Task UpdateAsync_StaleVersion_ThrowsConflictAndPreservesRegistration()
    {
        using var s = await Scope.CreateAsync();
        await s.AddRegistrationAsync();
        await Assert.ThrowsAsync<ConflictException>(() =>
            s.Service.UpdateAsync(ReceptionActor(), RegistrationId, s.UpdateRequest(StaleVersion()), default));
        await s.AssertUnchangedAsync();
    }

    [Fact]
    [Trait("Module", "ExaminationRegistration")]
    [Trait("Method", "UpdateAsync")]
    [Trait("TestCaseId", "UTC-REG-MAIN-010")]
    public async Task UpdateAsync_InvalidDoctor_ThrowsBadRequestAndPreservesRegistration()
    {
        using var s = await Scope.CreateAsync();
        await s.AddRegistrationAsync();
        var request = s.UpdateRequest();
        request.DoctorId = 999;
        await Assert.ThrowsAsync<BadRequestException>(() =>
            s.Service.UpdateAsync(ReceptionActor(), RegistrationId, request, default));
        await s.AssertUnchangedAsync();
    }

    #endregion

    #region ExaminationService.CancelAsync

    [Fact]
    [Trait("Module", "ExaminationRegistration")]
    [Trait("Method", "CancelAsync")]
    [Trait("TestCaseId", "UTC-REG-MAIN-011")]
    public async Task CancelAsync_ValidWaitingRegistration_CancelsAndRetainsAuditHistory()
    {
        using var s = await Scope.CreateAsync();
        await s.AddRegistrationAsync();
        var result = await s.Service.CancelAsync(ReceptionActor(), RegistrationId, Scope.CancelRequest(), default);
        var saved = await s.ReloadAsync();
        Assert.Equal("CANCELLED", result.Status);
        Assert.Equal("CANCELLED", saved.Status);
        Assert.NotNull(saved.CancelledAt);
        Assert.NotNull(saved.UpdatedAt);
        Assert.Equal(saved.CancelledAt, saved.UpdatedAt);
        Assert.Contains("Original note", saved.Note!);
        Assert.Contains("Patient requested cancellation", saved.Note!);
        Assert.Contains("user 100", saved.Note!);
        Assert.Equal(1, saved.PatientId);
        Assert.Equal(1, saved.DoctorId);
        Assert.Equal(1, saved.QueueNumber);
        Assert.Equal(1, await s.Db.ExaminationRegistrations.CountAsync());
        Assert.Equal(1, await s.Db.Patients.CountAsync());
        Assert.Equal(1, (await s.Db.ExaminationQueueCounters.AsNoTracking().SingleAsync()).LastNumber);
        Assert.Empty(await s.Db.MedicalRecords.ToListAsync());
    }

    [Fact]
    [Trait("Module", "ExaminationRegistration")]
    [Trait("Method", "CancelAsync")]
    [Trait("TestCaseId", "UTC-REG-MAIN-012")]
    public async Task CancelAsync_BlankReason_ThrowsBadRequestAndPreservesRegistration()
    {
        using var s = await Scope.CreateAsync();
        await s.AddRegistrationAsync();
        await Assert.ThrowsAsync<BadRequestException>(() =>
            s.Service.CancelAsync(ReceptionActor(), RegistrationId, Scope.CancelRequest("   "), default));
        await s.AssertUnchangedAsync();
    }

    [Fact]
    [Trait("Module", "ExaminationRegistration")]
    [Trait("Method", "CancelAsync")]
    [Trait("TestCaseId", "UTC-REG-MAIN-013")]
    public async Task CancelAsync_StartedRegistration_ThrowsConflictAndPreservesRegistration()
    {
        using var s = await Scope.CreateAsync();
        await s.AddRegistrationAsync("IN_PROGRESS");
        var startedAt = (await s.Db.ExaminationRegistrations.SingleAsync()).StartedAt;
        await Assert.ThrowsAsync<ConflictException>(() =>
            s.Service.CancelAsync(ReceptionActor(), RegistrationId, Scope.CancelRequest(), default));
        await s.AssertUnchangedAsync("IN_PROGRESS");
        Assert.Equal(startedAt, (await s.ReloadAsync()).StartedAt);
    }

    [Fact]
    [Trait("Module", "ExaminationRegistration")]
    [Trait("Method", "CancelAsync")]
    [Trait("TestCaseId", "UTC-REG-MAIN-014")]
    public async Task CancelAsync_UnknownRegistration_ThrowsNotFoundWithoutCreatingRegistration()
    {
        using var s = await Scope.CreateAsync();
        await Assert.ThrowsAsync<NotFoundException>(() =>
            s.Service.CancelAsync(ReceptionActor(), 999, Scope.CancelRequest(), default));
        s.Db.ChangeTracker.Clear();
        Assert.Empty(await s.Db.ExaminationRegistrations.ToListAsync());
        Assert.Empty(await s.Db.ExaminationQueueCounters.ToListAsync());
    }

    [Fact]
    [Trait("Module", "ExaminationRegistration")]
    [Trait("Method", "CancelAsync")]
    [Trait("TestCaseId", "UTC-REG-MAIN-015")]
    public async Task CancelAsync_StaleVersion_ThrowsConflictAndPreservesRegistration()
    {
        using var s = await Scope.CreateAsync();
        await s.AddRegistrationAsync();
        await Assert.ThrowsAsync<ConflictException>(() =>
            s.Service.CancelAsync(ReceptionActor(), RegistrationId,
                Scope.CancelRequest(version: StaleVersion()), default));
        await s.AssertUnchangedAsync();
    }

    #endregion
}

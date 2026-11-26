using COSGeniusSystem.Commons;
using COSGeniusSystem.Data;
using COSGeniusSystem.DTOs;
using COSGeniusSystem.Exceptions;
using COSGeniusSystem.Models;
using COSGeniusSystem.Services;
using COSGeniusSystem.Services.Herbs;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using System.Security.Claims;

namespace COSGeniusSystem.UnitTests;

public class ExaminationModuleTests
{
    private static DateOnly Today => DateOnly.FromDateTime(DateTime.UtcNow.AddHours(7));
    private static ClaimsPrincipal DoctorActor(int userId = 1) => new(new ClaimsIdentity(new[]
    {
        new Claim(ClaimTypes.NameIdentifier, userId.ToString()),
        new Claim(ClaimTypes.Role, RoleConstants.Doctor)
    }, "test"));

    private static byte[] Stale(byte[] actual)
    {
        var value = (byte[])actual.Clone();
        value[0] ^= 0xFF;
        return value;
    }

    private sealed class Scope : IDisposable
    {
        public ApplicationDbContext Db { get; }
        public ExaminationService Api { get; }
        public MedicalRecord Record { get; }
        public ExaminationRegistration Registration => Record.Registration;

        private Scope()
        {
            Db = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseInMemoryDatabase($"ExaminationTests_{Guid.NewGuid():N}")
                .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning)).Options);
            // InMemory has no SQL Server rowversion generator. Only initialize NEW rows;
            // never advance versions or simulate SQL concurrency on updates.
            Db.SavingChanges += (_, _) =>
            {
                Db.ChangeTracker.DetectChanges();
                foreach (var entry in Db.ChangeTracker.Entries().Where(e => e.State == EntityState.Added))
                {
                    var property = entry.Metadata.FindProperty("RowVersion");
                    if (property?.ClrType == typeof(byte[])
                        && (entry.Property("RowVersion").CurrentValue is not byte[] bytes || bytes.Length == 0))
                        entry.Property("RowVersion").CurrentValue = new byte[8];
                }
            };
            Record = SchemaFixture.Record();
            Record.Doctor.IsActive = true;
            Record.Doctor.User!.IsActive = true;
            Record.Status = Registration.Status = "IN_PROGRESS";
            Record.Symptoms = "Headache";
            Record.FinalDiagnosis = "Clinical diagnosis";
            Record.RowVersion = new byte[8];
            Registration.RowVersion = new byte[8];
            Registration.BusinessDate = Today;
            Registration.Session = "AFTERNOON";
            Registration.StartedAt = DateTime.UtcNow;
            Registration.CompletedAt = null;
            Record.CompletedAt = null;
            Db.MedicalRecords.Add(Record);
            Api = new ExaminationService(Db, new PatientService(Db));
        }

        public static async Task<Scope> CreateAsync()
        {
            var scope = new Scope();
            await scope.Db.SaveChangesAsync();
            return scope;
        }

        public async Task WaitingAsync()
        {
            Db.MedicalRecords.Remove(Record);
            await Db.SaveChangesAsync();
            Registration.MedicalRecord = null;
            Registration.Status = "WAITING";
            Registration.StartedAt = Registration.CompletedAt = null;
            await Db.SaveChangesAsync();
            Assert.False(await Db.MedicalRecords.AnyAsync());
        }

        public async Task CompletedAsync()
        {
            Record.Status = Registration.Status = "COMPLETED";
            Record.CompletedAt = Registration.CompletedAt = DateTime.UtcNow;
            await Db.SaveChangesAsync();
        }

        public Herb Herb(int id = 1)
        {
            var existing = Db.Herbs.Local.SingleOrDefault(x => x.HerbId == id);
            if (existing != null) return existing;
            var herb = new Herb
            {
                HerbId = id,
                HerbCode = $"H{id}",
                CommonName = $"Herb {id}",
                Unit = "g",
                StandardPrice = 1000m,
                IsActive = true
            };
            Db.Herbs.Add(herb);
            return herb;
        }

        public async Task<HerbalPrescription> PrescriptionAsync(bool withItems = true, decimal stock = 100m)
        {
            var herb = Herb();
            Db.Batches.Add(new Batch
            {
                BatchId = 1,
                HerbId = herb.HerbId,
                BatchNumber = "B01",
                OriginalQuantity = stock,
                CurrentQuantity = stock,
                ExpiryDate = Today.AddYears(1),
                Status = "Đạt chuẩn"
            });
            var rx = new HerbalPrescription
            {
                PrescriptionId = 1,
                Record = Record,
                CreatedByNavigation = Record.Doctor.User!,
                TotalDoses = 2,
                Status = "DRAFT",
                UsageInstructions = "Use as directed",
                RowVersion = new byte[8]
            };
            if (withItems) rx.HerbalPrescriptionDetails.Add(new HerbalPrescriptionDetail
            { HerbId = herb.HerbId, HerbNameSnapshot = herb.CommonName, DosagePerDose = 10m });
            Db.HerbalPrescriptions.Add(rx);
            await Db.SaveChangesAsync();
            return rx;
        }

        public async Task<TreatmentPlan> PlanAsync(bool withItems = true)
        {
            Db.Services.Add(new COSGeniusSystem.Models.Service
            {
                ServiceId = 1,
                ServiceCode = "S1",
                ServiceName = "Acupuncture",
                Unit = "session",
                IsActive = true,
                Price = 120000m
            });
            var plan = new TreatmentPlan
            {
                PlanId = 1,
                RecordId = Record.RecordId,
                DoctorId = Record.DoctorId,
                PlanName = "Treatment",
                TreatmentType = "SINGLE",
                PlannedSessionCount = 1,
                Status = "DRAFT",
                RowVersion = new byte[8]
            };
            if (withItems) plan.TreatmentPlanDetails.Add(new TreatmentPlanDetail
            {
                ServiceId = 1,
                ServiceNameSnapshot = "Acupuncture",
                UnitSnapshot = "session",
                UnitPrice = 120000m,
                PlannedQuantity = 1
            });
            Db.TreatmentPlans.Add(plan);
            await Db.SaveChangesAsync();
            return plan;
        }

        public async Task<HerbalFormula> FormulaAsync(int id = 1, string status = "APPROVED", bool active = true)
        {
            var herb = Herb();
            var formula = new HerbalFormula
            {
                HerbalFormulaId = id,
                FormulaCode = $"F{id}",
                FormulaName = $"Formula {id}",
                UsageInstructions = "Formula instructions",
                Status = status,
                IsActive = active,
                RowVersion = new byte[8]
            };
            formula.HerbalFormulaDetails.Add(new HerbalFormulaDetail
            { Herb = herb, DosagePerDose = 12m, RoleInFormula = "Main", Note = "Formula note" });
            Db.HerbalFormulas.Add(formula);
            await Db.SaveChangesAsync();
            return formula;
        }

        public SaveMedicalRecordRequest Findings(byte[]? version = null) => new()
        {
            RowVersion = version ?? Record.RowVersion,
            Symptoms = "Updated symptoms",
            FinalDiagnosis = "Updated diagnosis",
            DoctorAdvice = "Rest",
            PulseRate = 80,
            Temperature = 37m,
            SpO2 = 98m,
            Inspection = "Inspection",
            ListeningSmelling = "Listening",
            Inquiry = "Inquiry",
            Palpation = "Palpation",
            TraditionalDiagnosis = "Traditional diagnosis"
        };

        public static SavePrescriptionRequest Medicine(HerbalPrescription rx) => new()
        {
            RowVersion = rx.RowVersion,
            PrescriptionName = "Edited prescription",
            TotalDoses = 3,
            UsageInstructions = "Edited instructions",
            Items = [new() { HerbId = 1, DosagePerDose = 12m }]
        };

        public static SaveTreatmentPlanRequest Treatment(TreatmentPlan plan) => new()
        {
            RowVersion = plan.RowVersion,
            PlanName = "Edited treatment",
            TreatmentType = "SINGLE",
            PlannedSessionCount = 1,
            Items = [new() { ServiceId = 1, PlannedQuantity = 1 }]
        };

        public void Dispose() => Db.Dispose();
    }

    #region GetAsync

    // UTC-EXA-001: Retrieve registration, patient and the current medical record.
    [Fact]
    public async Task GetAsync_AssignedDoctor_ReturnsExamination()
    {
        using var s = await Scope.CreateAsync();
        var result = await s.Api.GetAsync(DoctorActor(), 1, default);
        Assert.Equal(1, result.Registration.RegistrationId);
        Assert.Equal(s.Record.PatientId, result.Patient.PatientId);
        Assert.Equal(s.Record.RecordId, result.MedicalRecord!.RecordId);
        Assert.Empty(result.Prescriptions);
        Assert.Empty(result.TreatmentPlans);
    }

    // UTC-EXA-002: Reject an examination registration that does not exist.
    [Fact]
    public async Task GetAsync_MissingRegistration_ThrowsNotFound()
    {
        using var s = await Scope.CreateAsync();
        await Assert.ThrowsAsync<NotFoundException>(() => s.Api.GetAsync(DoctorActor(), 999, default));
    }

    // UTC-EXA-003: Prevent an unauthorized doctor from reading the examination.
    [Fact]
    public async Task GetAsync_UnauthorizedDoctor_ThrowsForbidden()
    {
        using var s = await Scope.CreateAsync();
        await Assert.ThrowsAsync<ForbiddenException>(() => s.Api.GetAsync(DoctorActor(99), 1, default));
    }

    #endregion

    #region HistoryAsync

    // UTC-EXA-004: Return the patient medical-record history.
    [Fact]
    public async Task HistoryAsync_ExistingRecord_ReturnsPatientHistory()
    {
        using var s = await Scope.CreateAsync();
        var result = await s.Api.HistoryAsync(DoctorActor(), 1, new PageQuery(), default);
        Assert.Equal(1, result.TotalCount);
        Assert.Equal(s.Record.PatientId, Assert.Single(result.Items).PatientId);
    }

    // UTC-EXA-005: Reject a page number below the allowed minimum.
    [Fact]
    public async Task HistoryAsync_InvalidPage_ThrowsBadRequest()
    {
        using var s = await Scope.CreateAsync();
        await Assert.ThrowsAsync<BadRequestException>(() => s.Api.HistoryAsync(DoctorActor(), 1,
            new PageQuery { PageNumber = 0 }, default));
    }

    // UTC-EXA-006: Protect the patient examination history from unauthorized access.
    [Fact]
    public async Task HistoryAsync_UnauthorizedDoctor_ThrowsForbidden()
    {
        using var s = await Scope.CreateAsync();
        await Assert.ThrowsAsync<ForbiddenException>(() => s.Api.HistoryAsync(DoctorActor(99), 1, new PageQuery(), default));
    }

    #endregion

    #region HistoryDetailAsync

    // UTC-EXA-007: Retrieve the details of a historical medical record for the same patient.
    [Fact]
    public async Task HistoryDetailAsync_MatchingPatient_ReturnsRecordDetails()
    {
        using var s = await Scope.CreateAsync();
        var result = await s.Api.HistoryDetailAsync(DoctorActor(), 1, s.Record.RecordId, default);
        Assert.Equal(s.Record.RecordId, result.MedicalRecord!.RecordId);
        Assert.Equal(s.Record.PatientId, result.Patient.PatientId);
    }

    // UTC-EXA-008: Prevent access to a historical record belonging to another patient.
    [Fact]
    public async Task HistoryDetailAsync_AnotherPatientRecord_ThrowsNotFound()
    {
        using var s = await Scope.CreateAsync();
        var other = SchemaFixture.Record(2);
        other.Registration.BusinessDate = Today;
        other.Status = other.Registration.Status = "COMPLETED";
        s.Db.MedicalRecords.Add(other);
        await s.Db.SaveChangesAsync();
        await Assert.ThrowsAsync<NotFoundException>(() => s.Api.HistoryDetailAsync(DoctorActor(), 1, other.RecordId, default));
    }

    // UTC-EXA-009: Reject unauthorized access to historical examination details.
    [Fact]
    public async Task HistoryDetailAsync_UnauthorizedDoctor_ThrowsForbidden()
    {
        using var s = await Scope.CreateAsync();
        await Assert.ThrowsAsync<ForbiddenException>(() => s.Api.HistoryDetailAsync(DoctorActor(99), 1, 1, default));
    }

    #endregion

    #region StartAsync

    // UTC-EXA-010: Start an eligible examination and copy symptoms and allergy information.
    [Fact]
    public async Task StartAsync_WaitingRegistration_CreatesRecord()
    {
        using var s = await Scope.CreateAsync();
        await s.WaitingAsync();
        s.Registration.Symptoms = "Headache at registration";
        s.Registration.Patient.AllergyNote = "Herbal allergy";
        await s.Db.SaveChangesAsync();
        var result = await s.Api.StartAsync(DoctorActor(), 1, default);
        Assert.Equal("IN_PROGRESS", result.Status);
        Assert.Equal("Headache at registration", result.Symptoms);
        Assert.Equal("Herbal allergy", result.AllergyNoteSnapshot);
        s.Db.ChangeTracker.Clear();
        Assert.Single(await s.Db.MedicalRecords.ToListAsync());
        var saved = await s.Db.ExaminationRegistrations.SingleAsync();
        Assert.Equal("IN_PROGRESS", saved.Status);
        Assert.NotNull(saved.StartedAt);
    }

    // UTC-EXA-011: Repeat the start operation without creating a duplicate record.
    [Fact]
    public async Task StartAsync_AlreadyStarted_ReturnsExistingRecord()
    {
        using var s = await Scope.CreateAsync();
        var result = await s.Api.StartAsync(DoctorActor(), 1, default);
        Assert.Equal(s.Record.RecordId, result.RecordId);
        Assert.Equal(1, await s.Db.MedicalRecords.CountAsync());
    }

    // UTC-EXA-012: Prevent starting a cancelled examination registration.
    [Fact]
    public async Task StartAsync_CancelledRegistration_ThrowsConflict()
    {
        using var s = await Scope.CreateAsync();
        await s.WaitingAsync();
        s.Registration.Status = "CANCELLED";
        await s.Db.SaveChangesAsync();
        await Assert.ThrowsAsync<ConflictException>(() => s.Api.StartAsync(DoctorActor(), 1, default));
        Assert.False(await s.Db.MedicalRecords.AnyAsync());
    }

    // UTC-EXA-013: Prevent an unauthorized doctor from starting the examination.
    [Fact]
    public async Task StartAsync_UnauthorizedDoctor_ThrowsForbidden()
    {
        using var s = await Scope.CreateAsync();
        await s.WaitingAsync();
        await Assert.ThrowsAsync<ForbiddenException>(() => s.Api.StartAsync(DoctorActor(99), 1, default));
        Assert.False(await s.Db.MedicalRecords.AnyAsync());
    }

    // UTC-EXA-014: Reject an existing medical record whose status differs from its registration.
    [Fact]
    public async Task StartAsync_InconsistentRecord_ThrowsConflict()
    {
        using var s = await Scope.CreateAsync();
        s.Record.Status = "COMPLETED";
        await s.Db.SaveChangesAsync();
        await Assert.ThrowsAsync<ConflictException>(() => s.Api.StartAsync(DoctorActor(), 1, default));
    }

    #endregion

    #region SaveAsync

    // UTC-EXA-015: Save symptoms, diagnoses, vital signs and traditional examination findings.
    [Fact]
    public async Task SaveAsync_ValidFindings_UpdatesClinicalFields()
    {
        using var s = await Scope.CreateAsync();
        var result = await s.Api.SaveAsync(DoctorActor(), 1, s.Findings(), default);
        Assert.Equal("Updated symptoms", result.Symptoms);
        Assert.Equal("Updated diagnosis", result.FinalDiagnosis);
        Assert.Equal("IN_PROGRESS", result.Status);
        s.Db.ChangeTracker.Clear();
        var saved = await s.Db.MedicalRecords.SingleAsync();
        Assert.Equal("Updated symptoms", saved.Symptoms);
        Assert.Equal("Rest", saved.DoctorAdvice);
        Assert.Equal((short)80, saved.PulseRate);
        Assert.Equal(37m, saved.Temperature);
        Assert.Equal("Inspection", saved.Inspection);
        Assert.Equal("Listening", saved.ListeningSmelling);
        Assert.Equal("Inquiry", saved.Inquiry);
        Assert.Equal("Palpation", saved.Palpation);
        Assert.Equal("Traditional diagnosis", saved.TraditionalDiagnosis);
    }

    // UTC-EXA-016: Reject a vital sign outside the allowed range without saving changes.
    [Fact]
    public async Task SaveAsync_InvalidVitalSign_ThrowsBadRequest()
    {
        using var s = await Scope.CreateAsync();
        var request = s.Findings(); request.Temperature = 51m;
        await Assert.ThrowsAsync<BadRequestException>(() => s.Api.SaveAsync(DoctorActor(), 1, request, default));
        s.Db.ChangeTracker.Clear();
        Assert.Equal("Headache", (await s.Db.MedicalRecords.SingleAsync()).Symptoms);
    }

    // UTC-EXA-017: Reject findings submitted with an outdated medical-record version.
    [Fact]
    public async Task SaveAsync_StaleVersion_ThrowsConflict()
    {
        using var s = await Scope.CreateAsync();
        await Assert.ThrowsAsync<ConflictException>(() => s.Api.SaveAsync(DoctorActor(), 1, s.Findings(Stale(s.Record.RowVersion)), default));
        s.Db.ChangeTracker.Clear();
        Assert.Equal("Headache", (await s.Db.MedicalRecords.SingleAsync()).Symptoms);
    }

    // UTC-EXA-018: Prevent changing findings after examination completion.
    [Fact]
    public async Task SaveAsync_CompletedExamination_ThrowsConflict()
    {
        using var s = await Scope.CreateAsync(); await s.CompletedAsync();
        await Assert.ThrowsAsync<ConflictException>(() => s.Api.SaveAsync(DoctorActor(), 1, s.Findings(), default));
    }

    // UTC-EXA-019: Prevent an unauthorized doctor from saving clinical findings.
    [Fact]
    public async Task SaveAsync_UnauthorizedDoctor_ThrowsForbidden()
    {
        using var s = await Scope.CreateAsync();
        await Assert.ThrowsAsync<ForbiddenException>(() => s.Api.SaveAsync(DoctorActor(99), 1, s.Findings(), default));
    }

    #endregion

    #region CompleteAsync

    // UTC-EXA-020: Complete the examination, publish prescription and service indications, and accrue an invoice.
    [Fact]
    public async Task CompleteAsync_ValidIndications_PublishesAndCompletes()
    {
        using var s = await Scope.CreateAsync();
        var rx = await s.PrescriptionAsync(); var plan = await s.PlanAsync();
        var result = await s.Api.CompleteAsync(DoctorActor(), 1, new VersionRequest { RowVersion = s.Record.RowVersion }, default);
        Assert.Equal("COMPLETED", result.Status); Assert.NotNull(result.CompletedAt);
        s.Db.ChangeTracker.Clear();
        Assert.Equal("COMPLETED", (await s.Db.ExaminationRegistrations.SingleAsync()).Status);
        var savedRx = await s.Db.HerbalPrescriptions.SingleAsync();
        Assert.Equal("SENT", savedRx.Status); Assert.NotNull(savedRx.SentAt);
        var savedPlan = await s.Db.TreatmentPlans.SingleAsync();
        Assert.Equal("CONFIRMED", savedPlan.Status); Assert.NotNull(savedPlan.ConfirmedAt);
        Assert.True(await s.Db.Invoices.AnyAsync(i => i.RecordId == result.RecordId));
        Assert.False(await s.Db.TreatmentSessions.AnyAsync());
        Assert.Equal(100m, (await s.Db.Batches.SingleAsync()).CurrentQuantity);
        var stock = await HerbStockHelper.GetStockMapAsync(s.Db, new[] { 1 });
        Assert.Equal(80m, stock[1].AvailableStock);
    }

    // UTC-EXA-021: Require a final diagnosis before completing the examination.
    [Fact]
    public async Task CompleteAsync_MissingDiagnosis_ThrowsBadRequest()
    {
        using var s = await Scope.CreateAsync(); s.Record.FinalDiagnosis = "";
        await s.Db.SaveChangesAsync();
        await Assert.ThrowsAsync<BadRequestException>(() => s.Api.CompleteAsync(DoctorActor(), 1,
            new VersionRequest { RowVersion = s.Record.RowVersion }, default));
        s.Db.ChangeTracker.Clear();
        Assert.Equal("IN_PROGRESS", (await s.Db.MedicalRecords.SingleAsync()).Status);
        Assert.False(await s.Db.Invoices.AnyAsync());
    }

    // UTC-EXA-022: Reject examination completion with an outdated record version.
    [Fact]
    public async Task CompleteAsync_StaleVersion_ThrowsConflict()
    {
        using var s = await Scope.CreateAsync();
        await Assert.ThrowsAsync<ConflictException>(() => s.Api.CompleteAsync(DoctorActor(), 1,
            new VersionRequest { RowVersion = Stale(s.Record.RowVersion) }, default));
        Assert.False(await s.Db.Invoices.AnyAsync());
    }

    // UTC-EXA-023: Allow examination completion with insufficient herbal stock and do not deduct physical stock.
    [Fact]
    public async Task CompleteAsync_StockShortage_DoesNotBlockCompletion()
    {
        using var s = await Scope.CreateAsync(); await s.PrescriptionAsync(stock: 15m);
        var result = await s.Api.CompleteAsync(DoctorActor(), 1, new VersionRequest { RowVersion = s.Record.RowVersion }, default);
        Assert.Equal("COMPLETED", result.Status);
        s.Db.ChangeTracker.Clear();
        Assert.Equal("SENT", (await s.Db.HerbalPrescriptions.SingleAsync()).Status);
        Assert.Equal(15m, (await s.Db.Batches.SingleAsync()).CurrentQuantity);
    }

    // UTC-EXA-024: Repeat completion without creating additional invoices.
    [Fact]
    public async Task CompleteAsync_RepeatedCompletion_DoesNotDuplicateInvoices()
    {
        using var s = await Scope.CreateAsync(); await s.PrescriptionAsync();
        var request = new VersionRequest { RowVersion = s.Record.RowVersion };
        await s.Api.CompleteAsync(DoctorActor(), 1, request, default);
        var before = await s.Db.Invoices.CountAsync(); Assert.True(before > 0);
        var result = await s.Api.CompleteAsync(DoctorActor(), 1, request, default);
        Assert.Equal("COMPLETED", result.Status);
        Assert.Equal(before, await s.Db.Invoices.CountAsync());
    }

    #endregion

    #region FormulasAsync

    // UTC-EXA-025: Return an empty formula page when no approved active formula exists.
    [Fact]
    public async Task FormulasAsync_EmptyDatabase_ReturnsEmptyPage()
    {
        using var s = await Scope.CreateAsync();
        var result = await s.Api.FormulasAsync(new PageQuery(), default);
        Assert.Empty(result.Items); Assert.Equal(0, result.TotalCount);
    }

    // UTC-EXA-026: Only list approved active formulas that match the search.
    [Fact]
    public async Task FormulasAsync_FiltersApprovedActiveAndSearch_ReturnsPage()
    {
        using var s = await Scope.CreateAsync();
        await s.FormulaAsync(1); await s.FormulaAsync(2, status: "DRAFT");
        await s.FormulaAsync(3, active: false); await s.FormulaAsync(4);
        var result = await s.Api.FormulasAsync(new PageQuery { Search = "Formula 1", PageSize = 1 }, default);
        Assert.Equal(1, result.TotalCount);
        var formula = Assert.Single(result.Items);
        Assert.Equal(1, formula.HerbalFormulaId);
        Assert.Equal(12m, Assert.Single(formula.Items).DosagePerDose);
    }

    // UTC-EXA-027: Reject a formula page size greater than the allowed maximum.
    [Fact]
    public async Task FormulasAsync_InvalidPageSize_ThrowsBadRequest()
    {
        using var s = await Scope.CreateAsync();
        await Assert.ThrowsAsync<BadRequestException>(() => s.Api.FormulasAsync(new PageQuery { PageSize = 101 }, default));
    }

    #endregion

    #region CreatePrescriptionAsync

    // UTC-EXA-028: Create an empty draft prescription with a default pack count.
    [Fact]
    public async Task CreatePrescriptionAsync_EditableExamination_CreatesDraft()
    {
        using var s = await Scope.CreateAsync();
        var result = await s.Api.CreatePrescriptionAsync(DoctorActor(), 1, default);
        Assert.Equal("DRAFT", result.Status); Assert.Equal(1, result.TotalDoses);
        Assert.Equal(s.Record.RecordId, result.RecordId); Assert.Empty(result.Items);
        s.Db.ChangeTracker.Clear();
        Assert.Equal(result.PrescriptionId, (await s.Db.HerbalPrescriptions.SingleAsync()).PrescriptionId);
        Assert.False(await s.Db.Invoices.AnyAsync());
    }

    // UTC-EXA-029: Prevent creating a prescription after the examination is completed.
    [Fact]
    public async Task CreatePrescriptionAsync_CompletedExamination_ThrowsConflict()
    {
        using var s = await Scope.CreateAsync(); await s.CompletedAsync();
        await Assert.ThrowsAsync<ConflictException>(() => s.Api.CreatePrescriptionAsync(DoctorActor(), 1, default));
        Assert.False(await s.Db.HerbalPrescriptions.AnyAsync());
    }

    // UTC-EXA-030: Prevent an unauthorized doctor from creating a prescription.
    [Fact]
    public async Task CreatePrescriptionAsync_UnauthorizedDoctor_ThrowsForbidden()
    {
        using var s = await Scope.CreateAsync();
        await Assert.ThrowsAsync<ForbiddenException>(() => s.Api.CreatePrescriptionAsync(DoctorActor(99), 1, default));
    }

    #endregion

    #region SavePrescriptionAsync

    // UTC-EXA-031: Save prescription quantities and instructions while preserving an existing detail identity.
    [Fact]
    public async Task SavePrescriptionAsync_ValidDraft_SavesQuantityAndInstructions()
    {
        using var s = await Scope.CreateAsync(); var rx = await s.PrescriptionAsync();
        var originalId = rx.HerbalPrescriptionDetails.Single().PrescriptionDetailId;
        var result = await s.Api.SavePrescriptionAsync(DoctorActor(), 1, rx.PrescriptionId, Scope.Medicine(rx), default);
        Assert.Equal(3, result.TotalDoses); Assert.Equal("Edited instructions", result.UsageInstructions);
        var item = Assert.Single(result.Items);
        Assert.Equal(12m, item.DosagePerDose); Assert.Equal(36m, item.TotalQuantity);
        Assert.Equal(originalId, item.PrescriptionDetailId);
        s.Db.ChangeTracker.Clear();
        Assert.Equal(3, (await s.Db.HerbalPrescriptions.SingleAsync()).TotalDoses);
        Assert.Equal(100m, (await s.Db.Batches.SingleAsync()).CurrentQuantity);
    }

    // UTC-EXA-032: Permit editing a sent prescription while its examination is still in progress.
    [Fact]
    public async Task SavePrescriptionAsync_SentPrescription_RemainsEditableUntilCompletion()
    {
        using var s = await Scope.CreateAsync(); var rx = await s.PrescriptionAsync();
        rx.Status = "SENT"; rx.SentAt = DateTime.UtcNow; await s.Db.SaveChangesAsync();
        var result = await s.Api.SavePrescriptionAsync(DoctorActor(), 1, rx.PrescriptionId, Scope.Medicine(rx), default);
        Assert.Equal(3, result.TotalDoses); Assert.Equal("SENT", result.Status);
        await s.CompletedAsync();
        await Assert.ThrowsAsync<ConflictException>(() => s.Api.SavePrescriptionAsync(DoctorActor(), 1,
            rx.PrescriptionId, Scope.Medicine(rx), default));
    }

    // UTC-EXA-033: Reject the same herb appearing more than once in a prescription.
    [Fact]
    public async Task SavePrescriptionAsync_DuplicateHerbs_ThrowsBadRequest()
    {
        using var s = await Scope.CreateAsync(); var rx = await s.PrescriptionAsync();
        var request = Scope.Medicine(rx); request.Items.Add(new() { HerbId = 1, DosagePerDose = 5m });
        await Assert.ThrowsAsync<BadRequestException>(() => s.Api.SavePrescriptionAsync(DoctorActor(), 1, 1, request, default));
        s.Db.ChangeTracker.Clear();
        Assert.Equal(2, (await s.Db.HerbalPrescriptions.SingleAsync()).TotalDoses);
    }

    // UTC-EXA-034: Reject a prescription containing an inactive herb.
    [Fact]
    public async Task SavePrescriptionAsync_InactiveHerb_ThrowsBadRequest()
    {
        using var s = await Scope.CreateAsync(); var rx = await s.PrescriptionAsync();
        s.Herb().IsActive = false; await s.Db.SaveChangesAsync();
        await Assert.ThrowsAsync<BadRequestException>(() => s.Api.SavePrescriptionAsync(DoctorActor(), 1, 1, Scope.Medicine(rx), default));
    }

    // UTC-EXA-035: Reject a prescription update using an outdated version.
    [Fact]
    public async Task SavePrescriptionAsync_StaleVersion_ThrowsConflict()
    {
        using var s = await Scope.CreateAsync(); var rx = await s.PrescriptionAsync();
        var request = Scope.Medicine(rx); request.RowVersion = Stale(rx.RowVersion);
        await Assert.ThrowsAsync<ConflictException>(() => s.Api.SavePrescriptionAsync(DoctorActor(), 1, 1, request, default));
    }

    #endregion

    #region LoadFormulaAsync

    // UTC-EXA-036: Apply an approved formula to the draft prescription.
    [Fact]
    public async Task LoadFormulaAsync_ApprovedFormula_CopiesHerbsAndInstructions()
    {
        using var s = await Scope.CreateAsync(); var rx = await s.PrescriptionAsync(withItems: false);
        var formula = await s.FormulaAsync();
        var result = await s.Api.LoadFormulaAsync(DoctorActor(), 1, 1,
            new LoadFormulaRequest { RowVersion = rx.RowVersion, HerbalFormulaId = formula.HerbalFormulaId }, default);
        Assert.Equal(formula.HerbalFormulaId, result.SourceHerbalFormulaId);
        Assert.Equal(formula.FormulaName, result.PrescriptionName);
        Assert.Equal(formula.UsageInstructions, result.UsageInstructions);
        Assert.Equal(12m, Assert.Single(result.Items).DosagePerDose);
        s.Db.ChangeTracker.Clear();
        Assert.Equal(formula.HerbalFormulaId, (await s.Db.HerbalPrescriptions.SingleAsync()).SourceHerbalFormulaId);
    }

    // UTC-EXA-037: Reject a formula that has not been approved.
    [Fact]
    public async Task LoadFormulaAsync_UnapprovedFormula_ThrowsBadRequest()
    {
        using var s = await Scope.CreateAsync(); var rx = await s.PrescriptionAsync();
        var formula = await s.FormulaAsync(status: "DRAFT");
        await Assert.ThrowsAsync<BadRequestException>(() => s.Api.LoadFormulaAsync(DoctorActor(), 1, 1,
            new LoadFormulaRequest { RowVersion = rx.RowVersion, HerbalFormulaId = formula.HerbalFormulaId }, default));
    }

    // UTC-EXA-038: Reject an approved formula containing an inactive herb.
    [Fact]
    public async Task LoadFormulaAsync_InactiveFormulaHerb_ThrowsBadRequest()
    {
        using var s = await Scope.CreateAsync(); var rx = await s.PrescriptionAsync();
        var formula = await s.FormulaAsync(); s.Herb().IsActive = false; await s.Db.SaveChangesAsync();
        await Assert.ThrowsAsync<BadRequestException>(() => s.Api.LoadFormulaAsync(DoctorActor(), 1, 1,
            new LoadFormulaRequest { RowVersion = rx.RowVersion, HerbalFormulaId = formula.HerbalFormulaId }, default));
    }

    // UTC-EXA-039: Reject applying a formula with an outdated prescription version.
    [Fact]
    public async Task LoadFormulaAsync_StaleVersion_ThrowsConflict()
    {
        using var s = await Scope.CreateAsync(); var rx = await s.PrescriptionAsync(); await s.FormulaAsync();
        await Assert.ThrowsAsync<ConflictException>(() => s.Api.LoadFormulaAsync(DoctorActor(), 1, 1,
            new LoadFormulaRequest { RowVersion = Stale(rx.RowVersion), HerbalFormulaId = 1 }, default));
    }

    #endregion

    #region SendPrescriptionAsync

    // UTC-EXA-040: Send a valid prescription and update calculated availability without deducting physical stock.
    [Fact]
    public async Task SendPrescriptionAsync_SufficientStock_MarksSent()
    {
        using var s = await Scope.CreateAsync(); var rx = await s.PrescriptionAsync();
        var result = await s.Api.SendPrescriptionAsync(DoctorActor(), 1, 1,
            new VersionRequest { RowVersion = rx.RowVersion }, default);
        Assert.Equal("SENT", result.Status); Assert.NotNull(result.SentAt);
        Assert.Equal(1000m, Assert.Single(result.Items).UnitPrice);
        var stock = await HerbStockHelper.GetStockMapAsync(s.Db, new[] { 1 });
        Assert.Equal(100m, stock[1].PhysicalStock); Assert.Equal(80m, stock[1].AvailableStock);
    }

    // UTC-EXA-041: Current source blocks standalone sending when the requested herb quantity exceeds available stock.
    [Fact]
    public async Task SendPrescriptionAsync_InsufficientStock_ThrowsConflict_CurrentContract()
    {
        using var s = await Scope.CreateAsync(); var rx = await s.PrescriptionAsync(stock: 15m);
        var error = await Assert.ThrowsAsync<ConflictException>(() => s.Api.SendPrescriptionAsync(DoctorActor(), 1, 1,
            new VersionRequest { RowVersion = rx.RowVersion }, default));
        Assert.Contains("Herb 1", error.Message); Assert.Contains("20", error.Message); Assert.Contains("15", error.Message);
        s.Db.ChangeTracker.Clear();
        Assert.Equal("DRAFT", (await s.Db.HerbalPrescriptions.SingleAsync()).Status);
        Assert.Equal(15m, (await s.Db.Batches.SingleAsync()).CurrentQuantity);
    }

    // UTC-EXA-042: Require usage instructions before sending a prescription.
    [Fact]
    public async Task SendPrescriptionAsync_MissingUsageInstructions_ThrowsBadRequest()
    {
        using var s = await Scope.CreateAsync(); var rx = await s.PrescriptionAsync();
        rx.UsageInstructions = ""; await s.Db.SaveChangesAsync();
        await Assert.ThrowsAsync<BadRequestException>(() => s.Api.SendPrescriptionAsync(DoctorActor(), 1, 1,
            new VersionRequest { RowVersion = rx.RowVersion }, default));
    }

    // UTC-EXA-043: Repeat sending without changing the sent timestamp or creating additional prescriptions.
    [Fact]
    public async Task SendPrescriptionAsync_AlreadySent_IsIdempotent()
    {
        using var s = await Scope.CreateAsync(); var rx = await s.PrescriptionAsync();
        var request = new VersionRequest { RowVersion = rx.RowVersion };
        var first = await s.Api.SendPrescriptionAsync(DoctorActor(), 1, 1, request, default);
        var second = await s.Api.SendPrescriptionAsync(DoctorActor(), 1, 1, request, default);
        Assert.Equal(first.SentAt, second.SentAt); Assert.Equal(first.PrescriptionId, second.PrescriptionId);
        Assert.Equal(1, await s.Db.HerbalPrescriptions.CountAsync());
    }

    // UTC-EXA-044: Reject sending a draft with an outdated version.
    [Fact]
    public async Task SendPrescriptionAsync_StaleVersion_ThrowsConflict()
    {
        using var s = await Scope.CreateAsync(); var rx = await s.PrescriptionAsync();
        await Assert.ThrowsAsync<ConflictException>(() => s.Api.SendPrescriptionAsync(DoctorActor(), 1, 1,
            new VersionRequest { RowVersion = Stale(rx.RowVersion) }, default));
    }

    #endregion

    #region DeletePrescriptionAsync

    // UTC-EXA-045: Delete a draft prescription and its herb details.
    [Fact]
    public async Task DeletePrescriptionAsync_Draft_RemovesPrescriptionAndDetails()
    {
        using var s = await Scope.CreateAsync(); var rx = await s.PrescriptionAsync();
        await s.Api.DeletePrescriptionAsync(DoctorActor(), 1, 1, new VersionRequest { RowVersion = rx.RowVersion }, default);
        s.Db.ChangeTracker.Clear();
        Assert.False(await s.Db.HerbalPrescriptions.AnyAsync());
        Assert.False(await s.Db.HerbalPrescriptionDetails.AnyAsync());
        Assert.Equal(100m, (await s.Db.Batches.SingleAsync()).CurrentQuantity);
    }

    // UTC-EXA-046: Prevent deletion of a sent prescription.
    [Fact]
    public async Task DeletePrescriptionAsync_SentPrescription_ThrowsConflict()
    {
        using var s = await Scope.CreateAsync(); var rx = await s.PrescriptionAsync();
        rx.Status = "SENT"; await s.Db.SaveChangesAsync();
        await Assert.ThrowsAsync<ConflictException>(() => s.Api.DeletePrescriptionAsync(DoctorActor(), 1, 1,
            new VersionRequest { RowVersion = rx.RowVersion }, default));
        Assert.Equal(1, await s.Db.HerbalPrescriptions.CountAsync());
    }

    // UTC-EXA-047: Reject deletion of a prescription that does not belong to the current examination.
    [Fact]
    public async Task DeletePrescriptionAsync_MissingPrescription_ThrowsNotFound()
    {
        using var s = await Scope.CreateAsync();
        await Assert.ThrowsAsync<NotFoundException>(() => s.Api.DeletePrescriptionAsync(DoctorActor(), 1, 999, null, default));
    }

    // UTC-EXA-048: Reject deletion when a supplied valid-length version is stale.
    [Fact]
    public async Task DeletePrescriptionAsync_StaleVersion_ThrowsConflict()
    {
        using var s = await Scope.CreateAsync(); var rx = await s.PrescriptionAsync();
        await Assert.ThrowsAsync<ConflictException>(() => s.Api.DeletePrescriptionAsync(DoctorActor(), 1, 1,
            new VersionRequest { RowVersion = Stale(rx.RowVersion) }, default));
    }

    #endregion

    #region CreatePlanAsync

    // UTC-EXA-049: Create a single-treatment draft plan with one planned session.
    [Fact]
    public async Task CreatePlanAsync_EditableExamination_CreatesDraft()
    {
        using var s = await Scope.CreateAsync();
        var result = await s.Api.CreatePlanAsync(DoctorActor(), 1, default);
        Assert.Equal("DRAFT", result.Status); Assert.Equal("SINGLE", result.TreatmentType);
        Assert.Equal(1, result.PlannedSessionCount); Assert.Equal(s.Record.RecordId, result.RecordId);
        Assert.Empty(result.Items); Assert.False(await s.Db.TreatmentSessions.AnyAsync());
    }

    // UTC-EXA-050: Return the existing plan instead of creating a duplicate.
    [Fact]
    public async Task CreatePlanAsync_ExistingPlan_ReturnsSamePlan()
    {
        using var s = await Scope.CreateAsync(); var plan = await s.PlanAsync();
        var first = await s.Api.CreatePlanAsync(DoctorActor(), 1, default);
        var second = await s.Api.CreatePlanAsync(DoctorActor(), 1, default);
        Assert.Equal(plan.PlanId, first.PlanId); Assert.Equal(first.PlanId, second.PlanId);
        Assert.Equal(1, await s.Db.TreatmentPlans.CountAsync());
    }

    // UTC-EXA-051: Prevent creating a new treatment plan after examination completion.
    [Fact]
    public async Task CreatePlanAsync_CompletedExaminationWithoutPlan_ThrowsConflict()
    {
        using var s = await Scope.CreateAsync(); await s.CompletedAsync();
        await Assert.ThrowsAsync<ConflictException>(() => s.Api.CreatePlanAsync(DoctorActor(), 1, default));
        Assert.False(await s.Db.TreatmentPlans.AnyAsync());
    }

    #endregion

    #region SavePlanAsync

    // UTC-EXA-052: Save a course with matching service quantities and planned session details.
    [Fact]
    public async Task SavePlanAsync_ValidCourse_SavesServicesAndSessionDetails()
    {
        using var s = await Scope.CreateAsync(); var plan = await s.PlanAsync();
        var request = Scope.Treatment(plan); request.TreatmentType = "COURSE"; request.PlannedSessionCount = 2;
        request.Items[0].PlannedQuantity = 2;
        request.Sessions = [new() { SessionNumber = 1, ServiceIds = [1] }, new() { SessionNumber = 2, ServiceIds = [1] }];
        var result = await s.Api.SavePlanAsync(DoctorActor(), 1, 1, request, default);
        Assert.Equal("COURSE", result.TreatmentType); Assert.Equal(2, result.PlannedSessionCount);
        Assert.Equal(2, Assert.Single(result.Items).PlannedQuantity); Assert.Equal(2, result.Sessions.Count);
        s.Db.ChangeTracker.Clear();
        var saved = await s.Db.TreatmentPlans.SingleAsync();
        Assert.Equal("Edited treatment", saved.PlanName); Assert.NotNull(saved.SessionDetailsJson);
        Assert.False(await s.Db.TreatmentSessions.AnyAsync());
    }

    // UTC-EXA-053: Permit editing a confirmed service indication while the examination is still in progress.
    [Fact]
    public async Task SavePlanAsync_ConfirmedPlan_RemainsEditableUntilCompletion()
    {
        using var s = await Scope.CreateAsync(); var plan = await s.PlanAsync();
        plan.Status = "CONFIRMED"; plan.ConfirmedAt = DateTime.UtcNow; await s.Db.SaveChangesAsync();
        var result = await s.Api.SavePlanAsync(DoctorActor(), 1, 1, Scope.Treatment(plan), default);
        Assert.Equal("Edited treatment", result.PlanName); Assert.Equal("CONFIRMED", result.Status);
        await Assert.ThrowsAsync<ForbiddenException>(() =>
            s.Api.SavePlanAsync(
                DoctorActor(99),
                1,
                1,
                Scope.Treatment(plan),
                default));
        await s.CompletedAsync();
        await Assert.ThrowsAsync<ConflictException>(() => s.Api.SavePlanAsync(DoctorActor(), 1, 1, Scope.Treatment(plan), default));
    }

    // UTC-EXA-054: Reject session details that do not fulfill the prescribed service quantities.
    [Fact]
    public async Task SavePlanAsync_InconsistentSessionQuantities_ThrowsBadRequest()
    {
        using var s = await Scope.CreateAsync(); var plan = await s.PlanAsync();
        var request = Scope.Treatment(plan); request.TreatmentType = "COURSE"; request.PlannedSessionCount = 2;
        request.Items[0].PlannedQuantity = 2;
        request.Sessions = [new() { SessionNumber = 1, ServiceIds = [1] }];
        await Assert.ThrowsAsync<BadRequestException>(() => s.Api.SavePlanAsync(DoctorActor(), 1, 1, request, default));
        s.Db.ChangeTracker.Clear();
        Assert.Equal(1, (await s.Db.TreatmentPlans.SingleAsync()).PlannedSessionCount);
    }

    // UTC-EXA-055: Reject a treatment indication containing an inactive service.
    [Fact]
    public async Task SavePlanAsync_InactiveService_ThrowsBadRequest()
    {
        using var s = await Scope.CreateAsync(); var plan = await s.PlanAsync();
        (await s.Db.Services.SingleAsync()).IsActive = false; await s.Db.SaveChangesAsync();
        await Assert.ThrowsAsync<BadRequestException>(() => s.Api.SavePlanAsync(DoctorActor(), 1, 1, Scope.Treatment(plan), default));
    }

    // UTC-EXA-056: Reject updating a treatment plan with an outdated version.
    [Fact]
    public async Task SavePlanAsync_StaleVersion_ThrowsConflict()
    {
        using var s = await Scope.CreateAsync(); var plan = await s.PlanAsync();
        var request = Scope.Treatment(plan); request.RowVersion = Stale(plan.RowVersion);
        await Assert.ThrowsAsync<ConflictException>(() => s.Api.SavePlanAsync(DoctorActor(), 1, 1, request, default));
    }

    #endregion

    #region ConfirmPlanAsync

    // UTC-EXA-057: Confirm the indication without scheduling sessions or charging the patient.
    [Fact]
    public async Task ConfirmPlanAsync_ValidPlan_ConfirmsWithoutCreatingSessions()
    {
        using var s = await Scope.CreateAsync(); var plan = await s.PlanAsync();
        var result = await s.Api.ConfirmPlanAsync(DoctorActor(), 1, 1, new VersionRequest { RowVersion = plan.RowVersion }, default);
        Assert.Equal("CONFIRMED", result.Status); Assert.NotNull(result.ConfirmedAt);
        s.Db.ChangeTracker.Clear();
        Assert.Equal("CONFIRMED", (await s.Db.TreatmentPlans.SingleAsync()).Status);
        Assert.False(await s.Db.TreatmentSessions.AnyAsync()); Assert.False(await s.Db.Invoices.AnyAsync());
    }

    // UTC-EXA-058: Require at least one service before confirming a treatment plan.
    [Fact]
    public async Task ConfirmPlanAsync_EmptyPlan_ThrowsBadRequest()
    {
        using var s = await Scope.CreateAsync(); var plan = await s.PlanAsync(withItems: false);
        await Assert.ThrowsAsync<BadRequestException>(() => s.Api.ConfirmPlanAsync(DoctorActor(), 1, 1,
            new VersionRequest { RowVersion = plan.RowVersion }, default));
        s.Db.ChangeTracker.Clear();
        Assert.Equal("DRAFT", (await s.Db.TreatmentPlans.SingleAsync()).Status);
    }

    // UTC-EXA-059: Repeat confirmation without changing its timestamp or creating sessions.
    [Fact]
    public async Task ConfirmPlanAsync_AlreadyConfirmed_IsIdempotent()
    {
        using var s = await Scope.CreateAsync(); var plan = await s.PlanAsync();
        var request = new VersionRequest { RowVersion = plan.RowVersion };
        var first = await s.Api.ConfirmPlanAsync(DoctorActor(), 1, 1, request, default);
        var second = await s.Api.ConfirmPlanAsync(DoctorActor(), 1, 1, request, default);
        Assert.Equal(first.ConfirmedAt, second.ConfirmedAt); Assert.False(await s.Db.TreatmentSessions.AnyAsync());
    }

    // UTC-EXA-060: Reject confirming a draft plan with an outdated version.
    [Fact]
    public async Task ConfirmPlanAsync_StaleVersion_ThrowsConflict()
    {
        using var s = await Scope.CreateAsync(); var plan = await s.PlanAsync();
        await Assert.ThrowsAsync<ConflictException>(() => s.Api.ConfirmPlanAsync(DoctorActor(), 1, 1,
            new VersionRequest { RowVersion = Stale(plan.RowVersion) }, default));
    }

    #endregion

    #region DeletePlanAsync

    // UTC-EXA-061: Delete a draft treatment plan and all its service details.
    [Fact]
    public async Task DeletePlanAsync_Draft_RemovesPlanAndDetails()
    {
        using var s = await Scope.CreateAsync(); var plan = await s.PlanAsync();
        await s.Api.DeletePlanAsync(DoctorActor(), 1, 1, new VersionRequest { RowVersion = plan.RowVersion }, default);
        s.Db.ChangeTracker.Clear();
        Assert.False(await s.Db.TreatmentPlans.AnyAsync()); Assert.False(await s.Db.TreatmentPlanDetails.AnyAsync());
    }

    // UTC-EXA-062: Prevent deleting a confirmed treatment indication.
    [Fact]
    public async Task DeletePlanAsync_ConfirmedPlan_ThrowsConflict()
    {
        using var s = await Scope.CreateAsync(); var plan = await s.PlanAsync();
        plan.Status = "CONFIRMED"; await s.Db.SaveChangesAsync();
        await Assert.ThrowsAsync<ConflictException>(() => s.Api.DeletePlanAsync(DoctorActor(), 1, 1,
            new VersionRequest { RowVersion = plan.RowVersion }, default));
        Assert.Equal(1, await s.Db.TreatmentPlans.CountAsync());
    }

    // UTC-EXA-063: Prevent deleting a draft plan that already has a treatment session.
    [Fact]
    public async Task DeletePlanAsync_ExistingSession_ThrowsConflict()
    {
        using var s = await Scope.CreateAsync(); var plan = await s.PlanAsync();
        s.Db.TreatmentSessions.Add(new TreatmentSession
        {
            Plan = plan,
            SessionNumber = 1,
            ScheduledDate = Today,
            Status = "REQUESTED"
        });
        await s.Db.SaveChangesAsync();
        await Assert.ThrowsAsync<ConflictException>(() => s.Api.DeletePlanAsync(DoctorActor(), 1, 1,
            new VersionRequest { RowVersion = plan.RowVersion }, default));
        Assert.Equal(1, await s.Db.TreatmentPlans.CountAsync());
    }

    // UTC-EXA-064: Reject deleting a draft plan with an outdated version.
    [Fact]
    public async Task DeletePlanAsync_StaleVersion_ThrowsConflict()
    {
        using var s = await Scope.CreateAsync(); var plan = await s.PlanAsync();
        await Assert.ThrowsAsync<ConflictException>(() => s.Api.DeletePlanAsync(DoctorActor(), 1, 1,
            new VersionRequest { RowVersion = Stale(plan.RowVersion) }, default));
    }

    #endregion

    #region HerbStockHelper.GetStockMapAsync

    // UTC-EXA-065: Preserve the previous stock regression: sent demand reduces available stock; drafts and the excluded prescription do not.
    [Fact]
    public async Task GetStockMapAsync_SentDemandCountsButDraftDoesNot_ExcludeSelfWorks()
    {
        using var s = await Scope.CreateAsync(); var rx = await s.PrescriptionAsync();
        rx.Status = "SENT"; rx.TotalDoses = 5; rx.HerbalPrescriptionDetails.Single().DosagePerDose = 6m;
        s.Db.HerbalPrescriptions.Add(new HerbalPrescription
        {
            Record = s.Record,
            Status = "DRAFT",
            CreatedByNavigation = s.Record.Doctor.User!,
            TotalDoses = 2,
            UsageInstructions = "Use",
            HerbalPrescriptionDetails = [new() { HerbId = 1, HerbNameSnapshot = "Herb 1", DosagePerDose = 10m }]
        });
        await s.Db.SaveChangesAsync();
        var all = await HerbStockHelper.GetStockMapAsync(s.Db, new[] { 1 });
        Assert.Equal(100m, all[1].PhysicalStock); Assert.Equal(30m, all[1].ReservedStock);
        Assert.Equal(70m, all[1].AvailableStock);
        var excluded = await HerbStockHelper.GetStockMapAsync(s.Db, new[] { 1 }, excludePrescriptionId: rx.PrescriptionId);
        Assert.Equal(0m, excluded[1].ReservedStock); Assert.Equal(100m, excluded[1].AvailableStock);
    }

    #endregion
}

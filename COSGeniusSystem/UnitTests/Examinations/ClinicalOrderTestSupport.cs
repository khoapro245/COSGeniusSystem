using System;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using COSGeniusSystem.Commons;
using COSGeniusSystem.Data;
using COSGeniusSystem.DTOs;
using COSGeniusSystem.Exceptions;
using COSGeniusSystem.Models;
using COSGeniusSystem.Services;
using COSGeniusSystem.Services.Herbs;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Xunit;

namespace COSGeniusSystem.UnitTests;

public abstract class ClinicalOrderTestSupport
{
    protected static DateOnly Today => DateOnly.FromDateTime(DateTime.UtcNow.AddHours(7));
    protected static ClaimsPrincipal DoctorActor(int userId = 1) => new(new ClaimsIdentity(new[]
    {
        new Claim(ClaimTypes.NameIdentifier, userId.ToString()),
        new Claim(ClaimTypes.Role, RoleConstants.Doctor)
    }, "test"));

    protected static byte[] Stale(byte[] actual)
    {
        var value = (byte[])actual.Clone();
        value[0] ^= 0xFF;
        return value;
    }

    private static MedicalRecord CreateRecordGraph()
    {
        var user = new User
        {
            UserId = 1, Username = "doctor.test", FullName = "Doctor Test",
            PasswordHash = "test-only", IsActive = true
        };
        user.UserRoles.Add(new UserRole
        {
            Role = new Role { RoleId = 1, RoleCode = RoleConstants.Doctor, RoleName = "Doctor" }
        });
        var doctor = new Doctor
        {
            DoctorId = 1, FullName = "Doctor Test", User = user,
            IsActive = true, LicenseNumber = "TEST-DOC-1"
        };
        var patient = new Patient
        {
            PatientId = 1, PatientCode = "BN001", FullName = "Patient One", Phone = "0912345678"
        };
        var registration = new ExaminationRegistration
        {
            RegistrationId = 1, Patient = patient, Doctor = doctor,
            RegisteredByNavigation = user, RegisteredAt = DateTime.UtcNow,
            BusinessDate = Today, Session = "AFTERNOON", QueueNumber = 1,
            Status = "IN_PROGRESS", RowVersion = new byte[8], StartedAt = DateTime.UtcNow
        };
        return new MedicalRecord
        {
            RecordId = 1, Registration = registration, Patient = patient, Doctor = doctor,
            ExamDate = DateTime.UtcNow, Status = "IN_PROGRESS", RowVersion = new byte[8],
            Symptoms = "Headache", FinalDiagnosis = "Clinical diagnosis"
        };
    }

    protected sealed class Scope : IDisposable
    {
        public ApplicationDbContext Db { get; }
        public ExaminationService Api { get; }
        public MedicalRecord Record { get; }
        public ExaminationRegistration Registration => Record.Registration;

        private Scope()
        {
            Db = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseInMemoryDatabase($"ClinicalOrderMain_{Guid.NewGuid():N}")
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
            Record = CreateRecordGraph();
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
            var herb = new Herb { HerbId = id, HerbCode = $"H{id}", CommonName = $"Herb {id}",
                Unit = "g", StandardPrice = 1000m, IsActive = true };
            Db.Herbs.Add(herb);
            return herb;
        }

        public async Task<HerbalPrescription> PrescriptionAsync(bool withItems = true, decimal stock = 100m)
        {
            var herb = Herb();
            Db.Batches.Add(new Batch { BatchId = 1, HerbId = herb.HerbId, BatchNumber = "B01",
                OriginalQuantity = stock, CurrentQuantity = stock,
                ExpiryDate = Today.AddYears(1), Status = "Đạt chuẩn" });
            var rx = new HerbalPrescription { PrescriptionId = 1, Record = Record,
                CreatedByNavigation = Record.Doctor.User!, TotalDoses = 2, Status = "DRAFT",
                UsageInstructions = "Use as directed", RowVersion = new byte[8] };
            if (withItems) rx.HerbalPrescriptionDetails.Add(new HerbalPrescriptionDetail
                { HerbId = herb.HerbId, HerbNameSnapshot = herb.CommonName, DosagePerDose = 10m });
            Db.HerbalPrescriptions.Add(rx);
            await Db.SaveChangesAsync();
            return rx;
        }

        public async Task<TreatmentPlan> PlanAsync(bool withItems = true)
        {
            Db.Services.Add(new COSGeniusSystem.Models.Service { ServiceId = 1, ServiceCode = "S1",
                ServiceName = "Acupuncture", Unit = "session", IsActive = true, Price = 120000m });
            var plan = new TreatmentPlan { PlanId = 1, RecordId = Record.RecordId,
                DoctorId = Record.DoctorId, PlanName = "Treatment", TreatmentType = "SINGLE",
                PlannedSessionCount = 1, Status = "DRAFT", RowVersion = new byte[8] };
            if (withItems) plan.TreatmentPlanDetails.Add(new TreatmentPlanDetail
                { ServiceId = 1, ServiceNameSnapshot = "Acupuncture", UnitSnapshot = "session",
                    UnitPrice = 120000m, PlannedQuantity = 1 });
            Db.TreatmentPlans.Add(plan);
            await Db.SaveChangesAsync();
            return plan;
        }

        public async Task<HerbalFormula> FormulaAsync(int id = 1, string status = "APPROVED", bool active = true)
        {
            var herb = Herb();
            var formula = new HerbalFormula { HerbalFormulaId = id, FormulaCode = $"F{id}",
                FormulaName = $"Formula {id}", UsageInstructions = "Formula instructions",
                Status = status, IsActive = active, RowVersion = new byte[8] };
            formula.HerbalFormulaDetails.Add(new HerbalFormulaDetail
                { Herb = herb, DosagePerDose = 12m, RoleInFormula = "Main", Note = "Formula note" });
            Db.HerbalFormulas.Add(formula);
            await Db.SaveChangesAsync();
            return formula;
        }

        public SaveMedicalRecordRequest Findings(byte[]? version = null) => new()
        {
            RowVersion = version ?? Record.RowVersion, Symptoms = "Updated symptoms",
            FinalDiagnosis = "Updated diagnosis", DoctorAdvice = "Rest",
            PulseRate = 80, Temperature = 37m, SpO2 = 98m,
            Inspection = "Inspection", ListeningSmelling = "Listening", Inquiry = "Inquiry",
            Palpation = "Palpation", TraditionalDiagnosis = "Traditional diagnosis"
        };

        public static SavePrescriptionRequest Medicine(HerbalPrescription rx) => new()
        {
            RowVersion = rx.RowVersion, PrescriptionName = "Edited prescription", TotalDoses = 3,
            UsageInstructions = "Edited instructions", Items = [new() { HerbId = 1, DosagePerDose = 12m }]
        };

        public static SaveTreatmentPlanRequest Treatment(TreatmentPlan plan) => new()
        {
            RowVersion = plan.RowVersion, PlanName = "Edited treatment", TreatmentType = "SINGLE",
            PlannedSessionCount = 1, Items = [new() { ServiceId = 1, PlannedQuantity = 1 }]
        };

        // Capture persisted state, not tracked objects changed by a rejected request.
        // This checks saved data only; it does not prove relational rollback.
        public async Task<string> OrdersSnapshotAsync()
        {
            Db.ChangeTracker.Clear();
            var prescriptions = await Db.HerbalPrescriptions.AsNoTracking()
                .OrderBy(p => p.PrescriptionId).Select(p => new
                {
                    p.PrescriptionId, p.RecordId, p.PrescriptionName, p.TotalDoses,
                    p.DosageDays, p.UsageInstructions, p.Note, p.Status,
                    p.SourceHerbalFormulaId, p.SentAt, p.UpdatedAt, p.RowVersion
                }).ToListAsync();
            var herbDetails = await Db.HerbalPrescriptionDetails.AsNoTracking()
                .OrderBy(d => d.PrescriptionDetailId).Select(d => new
                {
                    d.PrescriptionDetailId, d.PrescriptionId, d.HerbId,
                    d.HerbNameSnapshot, d.DosagePerDose, d.RoleInFormula, d.Note
                }).ToListAsync();
            var plans = await Db.TreatmentPlans.AsNoTracking()
                .OrderBy(p => p.PlanId).Select(p => new
                {
                    p.PlanId, p.RecordId, p.DoctorId, p.PlanName, p.TreatmentType,
                    p.PlannedSessionCount, p.Description, p.Status, p.ConfirmedAt,
                    p.UpdatedAt, p.RowVersion, p.SessionDetailsJson
                }).ToListAsync();
            var serviceDetails = await Db.TreatmentPlanDetails.AsNoTracking()
                .OrderBy(d => d.PlanDetailId).Select(d => new
                {
                    d.PlanDetailId, d.PlanId, d.ServiceId, d.ServiceNameSnapshot,
                    d.UnitSnapshot, d.UnitPrice, d.PlannedQuantity, d.Instructions
                }).ToListAsync();
            var batches = await Db.Batches.AsNoTracking().OrderBy(b => b.BatchId)
                .Select(b => new { b.BatchId, b.CurrentQuantity }).ToListAsync();
            return System.Text.Json.JsonSerializer.Serialize(new
            {
                prescriptions, herbDetails, plans, serviceDetails, batches,
                InvoiceCount = await Db.Invoices.CountAsync(),
                SessionCount = await Db.TreatmentSessions.CountAsync(),
                UsageCount = await Db.UsageHistories.CountAsync()
            });
        }

        public void Dispose() => Db.Dispose();
    }

}

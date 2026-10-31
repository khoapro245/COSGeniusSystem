using System.Security.Claims;
using COSGeniusSystem.Common;
using COSGeniusSystem.Data;
using COSGeniusSystem.DTOs.Examinations;
using COSGeniusSystem.Exceptions;
using COSGeniusSystem.Models;
using COSGeniusSystem.Services;
using COSGeniusSystem.Services.Examinations;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

// Never load appsettings.json or fall back to the application's database.
var connection = Environment.GetEnvironmentVariable("GENIUS_EXAM_TEST_CONNECTION");
if (string.IsNullOrWhiteSpace(connection))
{
    Console.WriteLine("SKIPPED: GENIUS_EXAM_TEST_CONNECTION must point to a dedicated V2 test database.");
    return 2;
}
var builder = new SqlConnectionStringBuilder(connection);
if (!builder.InitialCatalog.StartsWith("GeniusExaminationTest_", StringComparison.Ordinal)
    || Environment.GetEnvironmentVariable("GENIUS_EXAM_ALLOW_TEST_WRITES") != builder.InitialCatalog)
    throw new InvalidOperationException("Refusing writes: test database name must start GeniusExaminationTest_ and match GENIUS_EXAM_ALLOW_TEST_WRITES.");
var options = new DbContextOptionsBuilder<ApplicationDbContext>().UseSqlServer(connection).Options;
var ct = CancellationToken.None;
var suffix = Guid.NewGuid().ToString("N")[..12];
var checks = 0;
void Check(bool condition, string message)
{
    if (!condition) throw new Exception("FAIL: " + message);
    checks++;
    Console.WriteLine("PASS: " + message);
}
async Task<T> Run<T>(Func<IExaminationService, Task<T>> action)
{
    await using var context = new ApplicationDbContext(options);
    return await action(new ExaminationService(context, new PatientService(context)));
}
async Task Status(int status, Func<Task> action)
{
    try { await action(); }
    catch (ApiException e) when (e.StatusCode == status) { Check(true, "Expected HTTP " + status); return; }
    throw new Exception("Expected HTTP " + status);
}
ClaimsPrincipal Actor(int id, string role) => new(new ClaimsIdentity(
    [new Claim(ClaimTypes.NameIdentifier, id.ToString()), new Claim(ClaimTypes.Role, role)], "Test"));

int patientId, doctorId, otherDoctorId, receptionId, doctorUserId, otherDoctorUserId, herbId, serviceId, formulaId;
await using (var db = new ApplicationDbContext(options))
{
    // Requires a provisioned clone with all V2 constraints/triggers. This suite never creates/migrates schema.
    Check(await db.Database.CanConnectAsync(ct), "Dedicated test database is reachable");
    var role = await db.Roles.SingleOrDefaultAsync(r => r.RoleCode == RoleConstants.Doctor, ct);
    if (role == null) { role = new Role { RoleCode = RoleConstants.Doctor, RoleName = "Doctor" }; db.Roles.Add(role); }
    User NewUser(string name) => new() { Username = name + suffix, FullName = name, PasswordHash = "TEST_ONLY_NOT_A_LOGIN", IsActive = true };
    var reception = NewUser("Reception");
    var doctorUser = NewUser("Doctor");
    var otherUser = NewUser("OtherDoctor");
    db.Users.AddRange(reception, doctorUser, otherUser);
    var doctor = new Doctor { User = doctorUser, FullName = "Doctor " + suffix, IsActive = true };
    var otherDoctor = new Doctor { User = otherUser, FullName = "Other doctor " + suffix, IsActive = true };
    doctorUser.UserRoles.Add(new UserRole { Role = role });
    otherUser.UserRoles.Add(new UserRole { Role = role });
    db.Doctors.AddRange(doctor, otherDoctor);
    var patient = new Patient { FullName = "Test " + suffix, AllergyNote = "Test allergy" };
    var herb = new Herb { HerbCode = "TEST_" + suffix, CommonName = "Test herb", Unit = "g", IsActive = true };
    var service = new Service { ServiceCode = "TEST_" + suffix, ServiceName = "Test service", Unit = "lần", Price = 125000, IsActive = true };
    db.Patients.Add(patient);
    db.Herbs.Add(herb);
    db.Services.Add(service);
    await db.SaveChangesAsync(ct);
    var formula = new HerbalFormula
    {
        FormulaName = "Test formula " + suffix, ProposedByDoctorId = doctor.DoctorId,
        Status = "APPROVED", IsActive = true, ReviewedBy = reception.UserId, ReviewedAt = DateTime.UtcNow,
        UsageInstructions = "Test instructions", CreatedAt = DateTime.UtcNow,
        HerbalFormulaDetails = [new HerbalFormulaDetail { HerbId = herb.HerbId, DosagePerDose = 8 }]
    };
    db.HerbalFormulas.Add(formula);
    await db.SaveChangesAsync(ct);
    (patientId, doctorId, otherDoctorId, receptionId, doctorUserId, otherDoctorUserId, herbId, serviceId, formulaId) =
        (patient.PatientId, doctor.DoctorId, otherDoctor.DoctorId, reception.UserId, doctorUser.UserId, otherUser.UserId, herb.HerbId, service.ServiceId, formula.HerbalFormulaId);
}
var receptionist = Actor(receptionId, RoleConstants.ReceptionStaff);
var clinician = Actor(doctorUserId, RoleConstants.Doctor);
var outsider = Actor(otherDoctorUserId, RoleConstants.Doctor);
Task<RegistrationResponse> Register() => Run(s => s.CreateAsync(receptionist, new() { PatientId = patientId, DoctorId = doctorId, Symptoms = "Test symptoms" }, ct));

var concurrent = await Task.WhenAll(Enumerable.Range(0, 12).Select(_ => Register()));
Check(concurrent.Select(r => (r.BusinessDate, r.QueueNumber)).Distinct().Count() == concurrent.Length, "Concurrent registration numbers are unique");
Check(concurrent.All(r => r.BusinessDate == DateOnly.FromDateTime(DateTime.UtcNow.AddHours(7))), "Queue date uses Vietnam UTC+7");
Check(concurrent.All(r => r.RegistrationCode == "KCB" + r.RegistrationId), "Registration code comes from computed column");
var first = concurrent[0];
var before = await Run(s => s.GetAsync(receptionist, first.RegistrationId, ct));
Check(before.MedicalRecord == null, "Reception does not create a medical record");
await Status(403, async () => { await Run(s => s.StartAsync(receptionist, first.RegistrationId, ct)); });
await Status(403, async () => { await Run(s => s.StartAsync(outsider, first.RegistrationId, ct)); });
var starts = await Task.WhenAll(Enumerable.Range(0, 8).Select(_ => Run(s => s.StartAsync(clinician, first.RegistrationId, ct))));
Check(starts.Select(r => r.RecordId).Distinct().Count() == 1, "Concurrent start creates exactly one record");
Check(starts.All(r => r.DoctorId == doctorId && r.AllergyNoteSnapshot == "Test allergy"), "UserId maps to Doctor.UserId and allergy is snapshotted");
await Status(409, async () => { await Run(s => s.UpdateAsync(receptionist, first.RegistrationId, new() { DoctorId = otherDoctorId, RowVersion = first.RowVersion }, ct)); });

foreach (var branch in new[] { "none", "medicine", "services", "both" })
{
    var registration = await Register();
    var record = await Run(s => s.StartAsync(clinician, registration.RegistrationId, ct));
    await Status(400, async () => { await Run(s => s.CompleteAsync(clinician, registration.RegistrationId, new() { RowVersion = record.RowVersion }, ct)); });
    var saved = await Run(s => s.SaveAsync(clinician, registration.RegistrationId,
        new() { RowVersion = record.RowVersion, Symptoms = "Test symptoms", FinalDiagnosis = "Test diagnosis", Icdcode = "M54" }, ct));
    await Status(409, async () => { await Run(s => s.SaveAsync(clinician, registration.RegistrationId,
        new() { RowVersion = record.RowVersion, Symptoms = "Stale" }, ct)); });
    PrescriptionResponse? sent = null;
    TreatmentPlanResponse? confirmed = null;
    if (branch is "medicine" or "both")
    {
        var drafts = await Task.WhenAll(Enumerable.Range(0, 5).Select(_ => Run(s => s.CreatePrescriptionAsync(clinician, registration.RegistrationId, ct))));
        Check(drafts.Select(p => p.PrescriptionId).Distinct().Count() == 1, "Repeated create reuses prescription");
        var draft = drafts[0];
        await Status(409, async () => { await Run(s => s.CompleteAsync(clinician, registration.RegistrationId, new() { RowVersion = saved.RowVersion }, ct)); });
        await Status(404, async () => { await Run(s => s.LoadFormulaAsync(clinician, first.RegistrationId, draft.PrescriptionId, new() { RowVersion = draft.RowVersion, HerbalFormulaId = formulaId }, ct)); });
        var loaded = await Run(s => s.LoadFormulaAsync(clinician, registration.RegistrationId, draft.PrescriptionId, new() { RowVersion = draft.RowVersion, HerbalFormulaId = formulaId }, ct));
        Check(loaded.Items.Single().DosagePerDose == 8, "Formula ingredients are copied");
        var edited = await Run(s => s.SavePrescriptionAsync(clinician, registration.RegistrationId, draft.PrescriptionId, new()
        {
            RowVersion = loaded.RowVersion, TotalDoses = 5, UsageInstructions = "Test usage",
            Items = [new() { HerbId = herbId, DosagePerDose = 6 }]
        }, ct));
        Check(edited.Items.Single().TotalQuantity == 30, "Total grams equals dosage times total doses");
        await using (var db = new ApplicationDbContext(options))
            Check(await db.HerbalFormulaDetails.Where(d => d.HerbalFormulaId == formulaId).Select(d => d.DosagePerDose).SingleAsync(ct) == 8, "Patient adjustments preserve formula template");
        sent = await Run(s => s.SendPrescriptionAsync(clinician, registration.RegistrationId, draft.PrescriptionId, new() { RowVersion = edited.RowVersion }, ct));
        var again = await Run(s => s.SendPrescriptionAsync(clinician, registration.RegistrationId, draft.PrescriptionId, new() { RowVersion = edited.RowVersion }, ct));
        Check(sent.Status == "SENT" && sent.SentAt == again.SentAt && sent.RowVersion.SequenceEqual(again.RowVersion), "Repeated send preserves original timestamp/version");
        await Status(409, async () => { await Run(s => s.SavePrescriptionAsync(clinician, registration.RegistrationId, draft.PrescriptionId, new() { RowVersion = sent.RowVersion }, ct)); });
    }
    if (branch is "services" or "both")
    {
        var drafts = await Task.WhenAll(Enumerable.Range(0, 5).Select(_ => Run(s => s.CreatePlanAsync(clinician, registration.RegistrationId, ct))));
        Check(drafts.Select(p => p.PlanId).Distinct().Count() == 1, "Repeated create reuses treatment order");
        var draft = drafts[0];
        await Status(409, async () => { await Run(s => s.CompleteAsync(clinician, registration.RegistrationId, new() { RowVersion = saved.RowVersion }, ct)); });
        await Status(400, async () => { await Run(s => s.SavePlanAsync(clinician, registration.RegistrationId, draft.PlanId,
            new() { RowVersion = draft.RowVersion, PlanName = "Invalid single", PlannedSessionCount = 2 }, ct)); });
        var edited = await Run(s => s.SavePlanAsync(clinician, registration.RegistrationId, draft.PlanId, new()
        {
            RowVersion = draft.RowVersion, PlanName = "Test course", TreatmentType = "COURSE", PlannedSessionCount = 5,
            Items = [new() { ServiceId = serviceId, PlannedQuantity = 5, Instructions = "Test instructions" }]
        }, ct));
        Check(edited.Items.Single().UnitPrice == 125000, "Price snapshot comes from backend");
        confirmed = await Run(s => s.ConfirmPlanAsync(clinician, registration.RegistrationId, draft.PlanId, new() { RowVersion = edited.RowVersion }, ct));
        var again = await Run(s => s.ConfirmPlanAsync(clinician, registration.RegistrationId, draft.PlanId, new() { RowVersion = edited.RowVersion }, ct));
        Check(confirmed.Status == "CONFIRMED" && confirmed.ConfirmedAt == again.ConfirmedAt, "Repeated confirmation preserves timestamp");
        await Status(409, async () => { await Run(s => s.SavePlanAsync(clinician, registration.RegistrationId, draft.PlanId,
            new() { RowVersion = confirmed.RowVersion, PlanName = "Locked" }, ct)); });
        await using var db = new ApplicationDbContext(options);
        Check(!await db.TreatmentSessions.AnyAsync(s => s.PlanId == draft.PlanId, ct), "Prescribing does not create treatment sessions");
        Check(!await db.UsageHistories.AnyAsync(u => u.PlanDetail.PlanId == draft.PlanId, ct), "Confirmation does not record service execution");
    }
    var completed = await Run(s => s.CompleteAsync(clinician, registration.RegistrationId, new() { RowVersion = saved.RowVersion }, ct));
    var repeated = await Run(s => s.CompleteAsync(clinician, registration.RegistrationId, new() { RowVersion = saved.RowVersion }, ct));
    var final = await Run(s => s.GetAsync(clinician, registration.RegistrationId, ct));
    Check(completed.Status == "COMPLETED" && final.Registration.Status == "COMPLETED"
        && completed.CompletedAt == final.Registration.CompletedAt && repeated.CompletedAt == completed.CompletedAt, "Atomic idempotent completion: " + branch);
    await Status(409, async () => { await Run(s => s.SaveAsync(clinician, registration.RegistrationId,
        new() { RowVersion = completed.RowVersion, Symptoms = "Locked" }, ct)); });
    Check(final.Prescriptions.Count == (sent == null ? 0 : 1) && final.TreatmentPlans.Count == (confirmed == null ? 0 : 1), "Parallel branches remain independent: " + branch);
}
Console.WriteLine($"{checks} SQL checks passed. Test fixtures tagged {suffix} remain only in the dedicated test database.");
return 0;

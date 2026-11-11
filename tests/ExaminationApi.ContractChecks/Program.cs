using System.ComponentModel.DataAnnotations;
using System.Reflection;
using System.Text.Json;
using COSGeniusSystem.Commons;
using COSGeniusSystem.DTOs;
using COSGeniusSystem.Controllers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

var root = Path.GetFullPath(args.FirstOrDefault() ?? ".");
var count = 0;
void Check(bool condition, string name)
{
    if (!condition) throw new Exception("FAIL: " + name);
    count++;
    Console.WriteLine("PASS: " + name);
}
bool Valid(object value) => Validator.TryValidateObject(value, new ValidationContext(value), [], true);

var sources = Directory.GetFiles(Path.Combine(root, "COSGeniusSystem"), "*.cs", SearchOption.AllDirectories)
    .Where(p => !p.Split(Path.DirectorySeparatorChar).Any(s => s is "bin" or "obj" or "UnitTests")).ToArray();
var errors = sources.SelectMany(p => CSharpSyntaxTree.ParseText(File.ReadAllText(p), path: p)
    .GetDiagnostics().Where(d => d.Severity == DiagnosticSeverity.Error)).ToArray();
foreach (var error in errors) Console.WriteLine(error);
Check(errors.Length == 0, $"C# syntax for {sources.Length} production source files (not a service build)");
Check(!Valid(new HerbalistSelection([])), "Herbalist rejects empty grouped request");
Check(!Valid(new HerbalistSelection(new int[101])), "Herbalist caps grouped request size");
Check(Valid(new HerbalistSelection([31, 32])), "Herbalist accepts grouped prescription IDs");
Check(!Valid(new HerbalistReceive("AAAAAAAAAAE=", [])), "Herbalist requires allocation reconciliation");
Check(!Valid(new HerbalistVersion("")), "Herbalist requires concurrency version");
Check(!Valid(new HerbalistFilter { PageSize = 101 }), "Herbalist caps pagination");
Check(!Valid(new HerbalistCreateRequest([31], new string('x', 2001))), "Herbalist note length matches SQL schema");

var controller = typeof(ExaminationsController);
var classRoles = controller.GetCustomAttribute<AuthorizeAttribute>()!.Roles!.Split(',');
Check(classRoles.Order().SequenceEqual(new[] { RoleConstants.Doctor, RoleConstants.ReceptionStaff }.Order()), "Controller requires real JWT roles");
foreach (var method in controller.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly))
{
    var role = method.GetCustomAttribute<AuthorizeAttribute>()?.Roles;
    var expected = method.Name is "Create" or "Update" or "Cancel" or "DoctorWorkload" ? RoleConstants.ReceptionStaff
        : method.Name is "Get" or "List" ? null : RoleConstants.Doctor;
    Check(role == expected, "Endpoint role: " + method.Name);
    Check(method.GetParameters().Any(p => p.ParameterType == typeof(CancellationToken)), "Cancellation: " + method.Name);
}
Check(!Valid(new CancelTreatment { RowVersion = new byte[8] }), "Cancellation requires a reason");
Check(!Valid(new CancelTreatment { RowVersion = new byte[7], Reason = "Waited too long" }), "Cancellation rejects stale token format");
Check(Valid(new CancelTreatment { RowVersion = new byte[8], Reason = "Waited too long" }), "Cancellation accepts reason and concurrency token");
Check(!Valid(new CreateRegistrationRequest()), "Reject missing patient/doctor IDs");
Check(!Valid(new UpdateRegistrationRequest { DoctorId = 1 }), "Update requires RowVersion");
Check(!Valid(new VersionRequest { RowVersion = new byte[7] }), "Reject malformed RowVersion length");
Check(Valid(new VersionRequest { RowVersion = new byte[8] }), "Accept SQL Server RowVersion length");
Check(!Valid(new CreateTreatmentRequest()), "Treatment queue rejects legacy body without plan/version");
Check(Valid(new CreateTreatmentRequest { PlanId = 1, AssignedDoctorId = 2,
    ScheduledDate = new DateOnly(2026, 11, 2), RowVersion = new byte[8] }), "Treatment queue accepts plan/session request");
Check(typeof(CreateTreatmentRequest).GetProperty("ExamSheetId") == null, "Treatment queue no longer accepts ExamSheetId");
Check(typeof(COSGeniusSystem.DTOs.InvoiceItemDto).GetProperty("ReferenceId") == null,
    "Invoice uses explicit prescription and plan detail references");
Check(typeof(COSGeniusSystem.DTOs.DashboardWaitingExamDto).GetProperty("RegistrationId") != null,
    "Dashboard identifies waiting registrations");
Check(typeof(COSGeniusSystem.DTOs.DashboardWaitingTreatmentDto).GetProperty("SessionId") != null,
    "Dashboard identifies requested sessions");
Check(typeof(TreatmentsController).GetCustomAttribute<AuthorizeAttribute>() != null, "Treatment compatibility routes require JWT");
Check(typeof(TreatmentsController).GetMethod("CreateTreatmentRequest")!.GetCustomAttribute<AuthorizeAttribute>()?.Roles
    == RoleConstants.ReceptionStaff, "Only reception can request a treatment session");
Check(!Valid(new RegistrationQuery { PageSize = 101 }), "Limit database pagination");
Check(!Valid(new RegistrationQuery { Status = "PAID" }), "Reject unsupported registration status");
Check(!Valid(new SaveMedicalRecordRequest { RowVersion = new byte[8], FinalDiagnosis = new string('x', 501) }), "Diagnosis length matches scaffold");
Check(!Valid(new SaveMedicalRecordRequest { RowVersion = new byte[8], SpO2 = 101 }), "Reject invalid body measurement");
Check(!Valid(new PrescriptionItemRequest { HerbId = 1, DosagePerDose = 0 }), "Reject zero herb dosage");
Check(!Valid(new SavePrescriptionRequest { RowVersion = new byte[8], TotalDoses = 0 }), "Reject zero total doses");
Check(!Valid(new PlanItemRequest { ServiceId = 1, PlannedQuantity = 0 }), "Reject zero service quantity");
Check(!Valid(new PlannedSessionRequest { SessionNumber = 0 }), "Reject invalid planned session number");
var plannedSessions = new List<PlannedSessionRequest>
{
    new() { SessionNumber = 1, ServiceIds = [10, 20] },
    new() { SessionNumber = 2, ServiceIds = [10] }
};
var persistedPlan = new TreatmentPlanResponse(1, "LT1", 1, 1, "Course", "COURSE", 2, "Note", "DRAFT",
    null, new byte[8], [], JsonSerializer.Serialize(plannedSessions));
using (var planJson = JsonDocument.Parse(JsonSerializer.Serialize(persistedPlan, new JsonSerializerOptions(JsonSerializerDefaults.Web))))
{
    Check(planJson.RootElement.GetProperty("sessions")[0].GetProperty("serviceIds").GetArrayLength() == 2
        && planJson.RootElement.GetProperty("sessions")[1].GetProperty("sessionNumber").GetInt32() == 2,
        "Persisted prescribing sessions round trip with multiple services per session");
    Check(!planJson.RootElement.TryGetProperty("sessionDetailsJson", out _), "Session storage format stays out of API response");
}
Check(!Valid(new SaveTreatmentPlanRequest { RowVersion = new byte[8], PlanName = "Test", TreatmentType = "PAID" }), "Reject invalid treatment type");
var jsonOptions = new JsonSerializerOptions(JsonSerializerDefaults.Web);
var version = JsonSerializer.Deserialize<VersionRequest>("{\"rowVersion\":\"AAAAAAAAAAE=\"}", jsonOptions)!;
Check(version.RowVersion.Length == 8 && version.RowVersion[7] == 1, "JSON RowVersion uses base64");
var envelope = JsonSerializer.Serialize(ApiResponseModel<object?>.Success(ExaminationMessages.Saved, null), jsonOptions);
using var parsed = JsonDocument.Parse(envelope);
Check(parsed.RootElement.EnumerateObject().Select(p => p.Name).Order().SequenceEqual(new[] { "code", "data", "isSuccess", "message" }.Order()), "Response envelope remains stable");
Console.WriteLine($"{count} checks passed. SQL transactions, row locks and EF query translation require the net10 SQL integration suite.");

using System.ComponentModel.DataAnnotations;
using System.Reflection;
using System.Text.Json;
using COSGeniusSystem.Commons;
using COSGeniusSystem.Commons;
using COSGeniusSystem.Controllers;
using COSGeniusSystem.DTOs.Examinations;
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

var controller = typeof(ExaminationsController);
var classRoles = controller.GetCustomAttribute<AuthorizeAttribute>()!.Roles!.Split(',');
Check(classRoles.Order().SequenceEqual(new[] { RoleConstants.Doctor, RoleConstants.ReceptionStaff }.Order()), "Controller requires real JWT roles");
foreach (var method in controller.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly))
{
    var role = method.GetCustomAttribute<AuthorizeAttribute>()?.Roles;
    var expected = method.Name is "Create" or "Update" ? RoleConstants.ReceptionStaff
        : method.Name is "Get" or "List" ? null : RoleConstants.Doctor;
    Check(role == expected, "Endpoint role: " + method.Name);
    Check(method.GetParameters().Any(p => p.ParameterType == typeof(CancellationToken)), "Cancellation: " + method.Name);
}
Check(!Valid(new CreateRegistrationRequest()), "Reject missing patient/doctor IDs");
Check(!Valid(new UpdateRegistrationRequest { DoctorId = 1 }), "Update requires RowVersion");
Check(!Valid(new VersionRequest { RowVersion = new byte[7] }), "Reject malformed RowVersion length");
Check(Valid(new VersionRequest { RowVersion = new byte[8] }), "Accept SQL Server RowVersion length");
Check(!Valid(new RegistrationQuery { PageSize = 101 }), "Limit database pagination");
Check(!Valid(new RegistrationQuery { Status = "PAID" }), "Reject unsupported registration status");
Check(!Valid(new SaveMedicalRecordRequest { RowVersion = new byte[8], FinalDiagnosis = new string('x', 501) }), "Diagnosis length matches scaffold");
Check(!Valid(new SaveMedicalRecordRequest { RowVersion = new byte[8], SpO2 = 101 }), "Reject invalid body measurement");
Check(!Valid(new PrescriptionItemRequest { HerbId = 1, DosagePerDose = 0 }), "Reject zero herb dosage");
Check(!Valid(new SavePrescriptionRequest { RowVersion = new byte[8], TotalDoses = 0 }), "Reject zero total doses");
Check(!Valid(new PlanItemRequest { ServiceId = 1, PlannedQuantity = 0 }), "Reject zero service quantity");
Check(!Valid(new SaveTreatmentPlanRequest { RowVersion = new byte[8], PlanName = "Test", TreatmentType = "PAID" }), "Reject invalid treatment type");
var jsonOptions = new JsonSerializerOptions(JsonSerializerDefaults.Web);
var version = JsonSerializer.Deserialize<VersionRequest>("{\"rowVersion\":\"AAAAAAAAAAE=\"}", jsonOptions)!;
Check(version.RowVersion.Length == 8 && version.RowVersion[7] == 1, "JSON RowVersion uses base64");
var envelope = JsonSerializer.Serialize(ApiResponseModel<object?>.Success(ExaminationMessages.Saved, null), jsonOptions);
using var parsed = JsonDocument.Parse(envelope);
Check(parsed.RootElement.EnumerateObject().Select(p => p.Name).Order().SequenceEqual(new[] { "code", "data", "isSuccess", "message" }.Order()), "Response envelope remains stable");
Console.WriteLine($"{count} checks passed. SQL transactions, row locks and EF query translation require the net10 SQL integration suite.");

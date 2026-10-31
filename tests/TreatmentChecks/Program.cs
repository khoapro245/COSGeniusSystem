using System.ComponentModel.DataAnnotations;
using System.Reflection;
using COSGeniusSystem.Common;
using COSGeniusSystem.Controllers;
using COSGeniusSystem.DTOs.Treatments;
using Microsoft.AspNetCore.Authorization;

var checks = 0;
void Check(bool condition, string message) { if (!condition) throw new Exception(message); checks++; Console.WriteLine("PASS: " + message); }
bool Valid(object value) => Validator.TryValidateObject(value, new ValidationContext(value), [], true);
Check(!Valid(new RequestTreatment()), "Request requires doctor, date and version");
Check(!Valid(new RequestTreatment { AssignedDoctorId = 1, ScheduledDate = DateOnly.MinValue, RowVersion = new byte[7] }), "Reject malformed concurrency token");
Check(Valid(new RequestTreatment { AssignedDoctorId = 1, ScheduledDate = DateOnly.FromDateTime(DateTime.UtcNow), RowVersion = new byte[8] }), "Accept complete request DTO");
Check(!Valid(new CompleteTreatment { RowVersion = new byte[8], ResultNote = "Result" }), "Reject empty service list");
Check(!Valid(new UsageInput { PlanDetailId = 1, Quantity = 0 }), "Reject zero service quantity");
Check(!Valid(new UsageInput { PlanDetailId = 1, Quantity = 1, ResultNote = new string('x', 501) }), "Usage note matches 500-character database column");
Check(!Valid(new CancelTreatment { RowVersion = new byte[8] }), "Cancellation needs a reason");
Check(!Valid(new SessionQuery { Status = "CONFIRMED" }), "Session and plan statuses are distinct");
Check(!Valid(new PlanQuery { PageSize = 101 }), "Limit list page size");
foreach (var type in new[] { typeof(TreatmentPlansController), typeof(TreatmentSessionsController) })
{
    Check(type.GetCustomAttribute<AuthorizeAttribute>()?.Roles == RoleConstants.ReceptionStaff + "," + RoleConstants.Doctor, type.Name + " enforces authentication and roles");
    foreach (var method in type.GetMethods(BindingFlags.DeclaredOnly | BindingFlags.Public | BindingFlags.Instance))
        Check(method.GetParameters().Any(p => p.ParameterType == typeof(CancellationToken)), method.Name + " supports cancellation");
}
Check(typeof(TreatmentSessionsController).GetMethod("Start")!.GetCustomAttribute<AuthorizeAttribute>()?.Roles == RoleConstants.Doctor, "Only doctor starts treatment");
Check(typeof(TreatmentSessionsController).GetMethod("Complete")!.GetCustomAttribute<AuthorizeAttribute>()?.Roles == RoleConstants.Doctor, "Only doctor completes treatment");
Check(typeof(TreatmentPlansController).GetMethod("CreateRequest")!.GetCustomAttribute<AuthorizeAttribute>()?.Roles == RoleConstants.ReceptionStaff, "Only reception creates requests");
Console.WriteLine($"{checks} checks passed; services also compiled against original models and cached EF9. SQL behavior and net10 build remain separate checks.");

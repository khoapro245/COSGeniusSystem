using System.Text.Json;
using COSGeniusSystem.Commons;
using COSGeniusSystem.Controllers;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using System.ComponentModel.DataAnnotations;
using COSGeniusSystem.DTOs.Patients.Requests;
using COSGeniusSystem.Exceptions;
using COSGeniusSystem.Models;
using COSGeniusSystem.Data;
using Microsoft.EntityFrameworkCore;
using COSGeniusSystem.Services;

using var db = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>()
    .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
db.Patients.AddRange(new Patient { PatientId = 1, FullName = "One" }, new Patient { PatientId = 2, FullName = "Two" });
await db.SaveChangesAsync();
var service = new PatientContactService(db);
var ct = CancellationToken.None;

await ExpectStatus(404, async () => { await service.CreateAsync(99, new() { FullName = "Relative" }, ct); });
Check(!await db.PatientContacts.AnyAsync(), "Missing parent must prevent contact creation");
var created = await service.CreateAsync(1, new() { FullName = " Relative ", Phone = "0123456789" }, ct);
Check(created.PatientId == 1 && created.FullName == "Relative", "Contact is assigned to the route patient");
await ExpectStatus(404, async () => { await service.GetByIdAsync(2, created.ContactId, ct); });
await ExpectStatus(404, () => service.UpdateAsync(2, created.ContactId, new() { FullName = "Wrong patient" }, ct));
await ExpectStatus(404, () => service.DeleteAsync(2, created.ContactId, ct));
Check(db.PatientContacts.Single().FullName == "Relative", "Cross-patient operations cannot change contacts");
await service.UpdateAsync(1, created.ContactId, new() { FullName = "Updated", Relationship = "Sibling" }, ct);
Check((await service.GetByIdAsync(1, created.ContactId, ct)).Relationship == "Sibling", "Contact update persists fields");
await service.DeleteAsync(1, created.ContactId, ct);
Check((await service.GetAllAsync(1, 1, 20, ct)).Items.Count == 0, "Deleted contact no longer appears");

var patientService = new PatientService(db);
var dependent = new PatientContact { PatientId = 1, FullName = "Dependent" };
db.PatientContacts.Add(dependent);
await db.SaveChangesAsync();
await ExpectStatus(409, () => patientService.DeleteAsync(1, ct));
Check(await db.Patients.AnyAsync(p => p.PatientId == 1), "Patient deletion with dependents is blocked");
db.PatientContacts.Remove(dependent);
await db.SaveChangesAsync();
await patientService.DeleteAsync(1, ct);
Check(!await db.Patients.AnyAsync(p => p.PatientId == 1), "Patient without dependents can be deleted");
db.Patients.Add(new Patient { PatientId = 1, FullName = "One" });
await db.SaveChangesAsync();
await ExpectStatus(400, async () => { await patientService.CreateAsync(new() { FullName = "Name", DateOfBirth = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(1) }, ct); });
await ExpectStatus(400, async () => { await patientService.GetAllAsync(0, 20, ct); });
await ExpectStatus(404, async () => { await patientService.GetByIdAsync(99, ct); });

foreach (var request in new object[]
{
    new CreatePatientRequest { FullName = "   " },
    new UpdatePatientRequest { FullName = new string('a', 151) },
    new CreatePatientRequest { FullName = "Name", Email = "invalid" },
    new CreatePatientContactRequest { FullName = "Relative", Phone = new string('1', 21) },
    new UpdatePatientContactRequest { FullName = "" }
})
    Check(!Validator.TryValidateObject(request, new ValidationContext(request), [], true), "Invalid DTO is rejected");
await service.CreateAsync(1, new() { FullName = "First" }, ct);
await service.CreateAsync(1, new() { FullName = "Second" }, ct);
var page = await service.GetAllAsync(1, 2, 1, ct);
Check(page.Items.Single().FullName == "Second" && page.TotalCount == 2 && page.TotalPages == 2 && page.HasPreviousPage && !page.HasNextPage, "Contact pagination includes correct items and totals");
await ExpectStatus(400, async () => { await service.GetAllAsync(1, 1, 101, ct); });

var patientPage = await patientService.GetAllAsync(2, 1, ct);
Check(patientPage.Items.Single().PatientId == 2 && patientPage.TotalCount == 2 && patientPage.TotalPages == 2, "Patient list includes page data and metadata");
var controller = new PatientContactsController(service);
var getResult = (ObjectResult)(await controller.GetAll(1, 1, 1, ct)).Result!;
var envelope = (ApiResponseModel<PagedResult<COSGeniusSystem.DTOs.Patients.Responses.PatientContactResponse>>)getResult.Value!;
Check(getResult.StatusCode == 200 && envelope.Code == 200 && envelope.IsSuccess && envelope.Data!.TotalCount == 2, "List uses the unified envelope");
var createResult = (CreatedAtActionResult)(await controller.Create(1, new() { FullName = "Third" }, ct)).Result!;
var createdEnvelope = (ApiResponseModel<COSGeniusSystem.DTOs.Patients.Responses.PatientContactResponse>)createResult.Value!;
Check(createResult.StatusCode == 201 && createdEnvelope.Code == 201 && createResult.ActionName == "GetById" && (int)createResult.RouteValues!["patientId"]! == 1, "Creation uses CreatedAtAction with matching code and route values");
var deleteResult = (ObjectResult)(await controller.Delete(1, createdEnvelope.Data!.ContactId, ct)).Result!;
var deleteEnvelope = (ApiResponseModel<object?>)deleteResult.Value!;
Check(deleteResult.StatusCode == 200 && deleteEnvelope.Code == 200 && deleteEnvelope.IsSuccess && deleteEnvelope.Data is null, "Contact delete returns 200 with wrapper");
var json = JsonSerializer.Serialize(deleteEnvelope);
Check(json.Contains("\"code\":200") && json.Contains("\"message\":") && json.Contains("\"isSuccess\":true") && json.Contains("\"data\":null"), "JSON fields use the required names and include null data");
var modelState = new ModelStateDictionary();
modelState.AddModelError("FullName", "FullName is required.");
modelState.AddModelError("Email", "Invalid email.");
var errors = ValidationErrorData.FromModelState(modelState);
Check(errors.Errors.Count == 2 && errors.Errors["FullName"].Length == 1, "Validation preserves individual field errors");
foreach (var exception in new ApiException[] { new BadRequestException(), new NotFoundException(), new UnauthorizedException(), new ForbiddenException(), new ConflictException(), new InternalServerErrorException() })
    Check(!exception.IsSuccess && exception.Code == exception.StatusCode, "Exceptions are always unsuccessful");
ExpectArgument(() => new NotFoundException(SystemMessages.PatientRetrieved));
ExpectArgument(() => new NotFoundException(new SystemMessage(400, "Wrong code", false)));
ExpectArgument(() => new ApiException(new SystemMessage(400, "Wrong flag", true)));
ExpectArgument(() => ApiResponseModel<object?>.Success(SystemMessages.ValidationFailed, null));
ExpectArgument(() => new ApiResponseModel<object?>(200, "Wrong flag", false, null));
Console.WriteLine("All patient service, response, controller and validation checks passed.");

static void ExpectArgument(Action action)
{
    try { action(); }
    catch (ArgumentException) { return; }
    throw new Exception("Expected invalid message or response to be rejected.");
}

static void Check(bool condition, string message)
{
    if (!condition) throw new Exception(message);
}
static async Task ExpectStatus(int status, Func<Task> action)
{
    try { await action(); }
    catch (ApiException ex) when (ex.StatusCode == status) { return; }
    throw new Exception($"Expected API status {status}.");
}

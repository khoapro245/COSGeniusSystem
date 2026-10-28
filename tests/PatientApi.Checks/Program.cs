using System.Text.Json;
using COSGeniusSystem.Commons;
using COSGeniusSystem.Controllers;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using System.ComponentModel.DataAnnotations;
using COSGeniusSystem.DTOs.Requests;
using COSGeniusSystem.Exceptions;
using COSGeniusSystem.Models;
using COSGeniusSystem.Repositories.Interfaces;
using COSGeniusSystem.Services;

var patients = new FakePatients();
var contacts = new FakeContacts();
var service = new PatientContactService(contacts, patients);
var ct = CancellationToken.None;

await ExpectStatus(404, async () => { await service.CreateAsync(99, new() { FullName = "Relative" }, ct); });
Check(contacts.Items.Count == 0, "Missing parent must prevent contact creation");
var created = await service.CreateAsync(1, new() { FullName = " Relative ", Phone = "0123456789" }, ct);
Check(created.PatientId == 1 && created.FullName == "Relative", "Contact is assigned to the route patient");
await ExpectStatus(404, async () => { await service.GetByIdAsync(2, created.ContactId, ct); });
await ExpectStatus(404, () => service.UpdateAsync(2, created.ContactId, new() { FullName = "Wrong patient" }, ct));
await ExpectStatus(404, () => service.DeleteAsync(2, created.ContactId, ct));
Check(contacts.Items.Single().FullName == "Relative", "Cross-patient operations cannot change contacts");
await service.UpdateAsync(1, created.ContactId, new() { FullName = "Updated", Relationship = "Sibling" }, ct);
Check((await service.GetByIdAsync(1, created.ContactId, ct)).Relationship == "Sibling", "Contact update persists fields");
await service.DeleteAsync(1, created.ContactId, ct);
Check((await service.GetAllAsync(1, 1, 20, ct)).Items.Count == 0, "Deleted contact no longer appears");

var patientService = new PatientService(patients);
patients.HasDependents = true;
await ExpectStatus(409, () => patientService.DeleteAsync(1, ct));
Check(!patients.Removed, "Patient deletion with dependents is blocked");
patients.HasDependents = false;
await patientService.DeleteAsync(1, ct);
Check(patients.Removed, "Patient without dependents can be deleted");
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
var envelope = (ApiResponseModel<PagedResult<COSGeniusSystem.DTOs.Responses.PatientContactResponse>>)getResult.Value!;
Check(getResult.StatusCode == 200 && envelope.Code == 200 && envelope.IsSuccess && envelope.Data!.TotalCount == 2, "List uses the unified envelope");
var createResult = (CreatedAtActionResult)(await controller.Create(1, new() { FullName = "Third" }, ct)).Result!;
var createdEnvelope = (ApiResponseModel<COSGeniusSystem.DTOs.Responses.PatientContactResponse>)createResult.Value!;
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

sealed class FakePatients : IPatientRepository
{
    public bool HasDependents { get; set; }
    public bool Removed { get; private set; }
    public Task<IReadOnlyList<Patient>> GetAllAsync(int pageNumber, int pageSize, CancellationToken ct) => Task.FromResult<IReadOnlyList<Patient>>(new[] { new Patient { PatientId = 1, FullName = "One" }, new Patient { PatientId = 2, FullName = "Two" } }.Skip((pageNumber - 1) * pageSize).Take(pageSize).ToList());
    public Task<Patient?> GetByIdAsync(int patientId, CancellationToken ct) => Task.FromResult<Patient?>(patientId is 1 or 2 ? new Patient { PatientId = patientId, FullName = "Patient" } : null);
    public Task<bool> ExistsAsync(int patientId, CancellationToken ct) => Task.FromResult(patientId is 1 or 2);
    public Task<bool> HasDependentsAsync(int patientId, CancellationToken ct) => Task.FromResult(HasDependents);
    public Task<int> CountAsync(CancellationToken ct) => Task.FromResult(2);
    public Task AddAsync(Patient entity, CancellationToken ct) => Task.CompletedTask;
    public void Remove(Patient entity) => Removed = true;
    public Task SaveChangesAsync(CancellationToken ct) => Task.CompletedTask;
}
sealed class FakeContacts : IPatientContactRepository
{
    public List<PatientContact> Items { get; } = [];
    public Task<IReadOnlyList<PatientContact>> GetAllAsync(int patientId, int pageNumber, int pageSize, CancellationToken ct) => Task.FromResult<IReadOnlyList<PatientContact>>(Items.Where(c => c.PatientId == patientId).Skip((pageNumber - 1) * pageSize).Take(pageSize).ToList());
    public Task<int> CountAsync(int patientId, CancellationToken ct) => Task.FromResult(Items.Count(c => c.PatientId == patientId));
    public Task<PatientContact?> GetByIdAsync(int patientId, int contactId, CancellationToken ct) => Task.FromResult(Items.SingleOrDefault(c => c.PatientId == patientId && c.ContactId == contactId));
    public Task AddAsync(PatientContact entity, CancellationToken ct)
    {
        entity.ContactId = Items.Count + 1;
        Items.Add(entity);
        return Task.CompletedTask;
    }
    public void Remove(PatientContact entity) => Items.Remove(entity);
    public Task SaveChangesAsync(CancellationToken ct) => Task.CompletedTask;
}

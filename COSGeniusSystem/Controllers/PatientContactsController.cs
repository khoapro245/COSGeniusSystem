using System.ComponentModel.DataAnnotations;
using COSGeniusSystem.Commons;
using COSGeniusSystem.DTOs.Requests;
using COSGeniusSystem.DTOs.Responses;
using COSGeniusSystem.Services.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace COSGeniusSystem.Controllers;

[Route("api/patients/{patientId:int:min(1)}/contacts")]
public sealed class PatientContactsController(IPatientContactService service) : BaseApiController
{
    [HttpGet]
    public async Task<ActionResult<ApiResponseModel<PagedResult<PatientContactResponse>>>> GetAll(
        int patientId, [FromQuery, Range(1, 1000000)] int pageNumber = 1,
        [FromQuery, Range(1, 100)] int pageSize = 20, CancellationToken ct = default) =>
        SuccessResponse(SystemMessages.ContactsRetrieved, await service.GetAllAsync(patientId, pageNumber, pageSize, ct));

    [HttpGet("{contactId:int:min(1)}")]
    public async Task<ActionResult<ApiResponseModel<PatientContactResponse>>> GetById(int patientId, int contactId, CancellationToken ct) =>
        SuccessResponse(SystemMessages.ContactRetrieved, await service.GetByIdAsync(patientId, contactId, ct));

    [HttpPost]
    public async Task<ActionResult<ApiResponseModel<PatientContactResponse>>> Create(int patientId, CreatePatientContactRequest request, CancellationToken ct)
    {
        var response = await service.CreateAsync(patientId, request, ct);
        return CreatedResponse(nameof(GetById), new { patientId, contactId = response.ContactId }, SystemMessages.ContactCreated, response);
    }

    [HttpPut("{contactId:int:min(1)}")]
    public async Task<ActionResult<ApiResponseModel<object?>>> Update(int patientId, int contactId, UpdatePatientContactRequest request, CancellationToken ct)
    {
        await service.UpdateAsync(patientId, contactId, request, ct);
        return SuccessResponse(SystemMessages.ContactUpdated);
    }

    [HttpDelete("{contactId:int:min(1)}")]
    public async Task<ActionResult<ApiResponseModel<object?>>> Delete(int patientId, int contactId, CancellationToken ct)
    {
        await service.DeleteAsync(patientId, contactId, ct);
        return SuccessResponse(SystemMessages.ContactDeleted);
    }
}

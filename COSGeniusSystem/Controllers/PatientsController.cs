using System.ComponentModel.DataAnnotations;
using COSGeniusSystem.Commons;
using COSGeniusSystem.DTOs.Requests;
using COSGeniusSystem.DTOs.Responses;
using COSGeniusSystem.Services.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace COSGeniusSystem.Controllers;

[Route("api/patients")]
public sealed class PatientsController(IPatientService service) : BaseApiController
{
    [HttpGet]
    public async Task<ActionResult<ApiResponseModel<PagedResult<PatientResponse>>>> GetAll(
        [FromQuery, Range(1, 1000000)] int pageNumber = 1,
        [FromQuery, Range(1, 100)] int pageSize = 20, CancellationToken ct = default) =>
        SuccessResponse(SystemMessages.PatientsRetrieved, await service.GetAllAsync(pageNumber, pageSize, ct));

    [HttpGet("{patientId:int:min(1)}")]
    public async Task<ActionResult<ApiResponseModel<PatientResponse>>> GetById(int patientId, CancellationToken ct) =>
        SuccessResponse(SystemMessages.PatientRetrieved, await service.GetByIdAsync(patientId, ct));

    [HttpPost]
    public async Task<ActionResult<ApiResponseModel<PatientResponse>>> Create(CreatePatientRequest request, CancellationToken ct)
    {
        var response = await service.CreateAsync(request, ct);
        return CreatedResponse(nameof(GetById), new { patientId = response.PatientId }, SystemMessages.PatientCreated, response);
    }

    [HttpPut("{patientId:int:min(1)}")]
    public async Task<ActionResult<ApiResponseModel<object?>>> Update(int patientId, UpdatePatientRequest request, CancellationToken ct)
    {
        await service.UpdateAsync(patientId, request, ct);
        return SuccessResponse(SystemMessages.PatientUpdated);
    }

    [HttpDelete("{patientId:int:min(1)}")]
    public async Task<ActionResult<ApiResponseModel<object?>>> Delete(int patientId, CancellationToken ct)
    {
        await service.DeleteAsync(patientId, ct);
        return SuccessResponse(SystemMessages.PatientDeleted);
    }
}

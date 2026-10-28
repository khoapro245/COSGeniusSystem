using System.ComponentModel.DataAnnotations;
using COSGeniusSystem.Commons;
using COSGeniusSystem.DTOs.Requests;
using COSGeniusSystem.DTOs.Responses;
using COSGeniusSystem.Services.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace COSGeniusSystem.Controllers
{
    [Route("api/patients")]
    public sealed class PatientsController : BaseApiController
    {
        private readonly IPatientService _patientService;

        public PatientsController(IPatientService patientService)
        {
            _patientService = patientService;
        }

        // GET: api/patients
        [HttpGet]
        public async Task<ActionResult<ApiResponseModel<PagedResult<PatientResponse>>>> GetAll(
            [FromQuery, Range(1, 1000000)] int pageNumber = 1,
            [FromQuery, Range(1, 100)] int pageSize = 20, CancellationToken ct = default)
        {
            var patients = await _patientService.GetAllAsync(pageNumber, pageSize, ct);
            return SuccessResponse(SystemMessages.PatientsRetrieved, patients);
        }

        // GET: api/patients/{patientId}
        [HttpGet("{patientId:int:min(1)}")]
        public async Task<ActionResult<ApiResponseModel<PatientResponse>>> GetById(int patientId, CancellationToken ct)
        {
            var patientResponse = await _patientService.GetByIdAsync(patientId, ct);
            return SuccessResponse(SystemMessages.PatientRetrieved, patientResponse);
        }

        // POST: api/patients
        [HttpPost]
        public async Task<ActionResult<ApiResponseModel<PatientResponse>>> Create([FromBody] CreatePatientRequest request, CancellationToken ct)
        {
            var response = await _patientService.CreateAsync(request, ct);
            return CreatedResponse(nameof(GetById), new { patientId = response.PatientId }, SystemMessages.PatientCreated, response);
        }

        // PUT: api/patients/{patientId}
        [HttpPut("{patientId:int:min(1)}")]
        public async Task<ActionResult<ApiResponseModel<object?>>> Update(int patientId, [FromBody] UpdatePatientRequest request, CancellationToken ct)
        {
            await _patientService.UpdateAsync(patientId, request, ct);
            return SuccessResponse(SystemMessages.PatientUpdated);
        }

        // DELETE: api/patients/{patientId}
        [HttpDelete("{patientId:int:min(1)}")]
        public async Task<ActionResult<ApiResponseModel<object?>>> Delete(int patientId, CancellationToken ct)
        {
            await _patientService.DeleteAsync(patientId, ct);
            return SuccessResponse(SystemMessages.PatientDeleted);
        }
    }
}

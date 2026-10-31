using System.ComponentModel.DataAnnotations;
using COSGeniusSystem.Commons;
using COSGeniusSystem.DTOs.Patients.Requests;
using COSGeniusSystem.DTOs.Patients.Responses;
using COSGeniusSystem.Services.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace COSGeniusSystem.Controllers
{
    [Route("api/patients/{patientId:int:min(1)}/contacts")]
    public sealed class PatientContactsController : BaseApiController
    {
        private readonly IPatientContactService _patientContactService;

        public PatientContactsController(IPatientContactService patientContactService)
        {
            _patientContactService = patientContactService;
        }

        // GET: api/patients/{patientId}/contacts
        [HttpGet]
        public async Task<ActionResult<ApiResponseModel<PagedResult<PatientContactResponse>>>> GetAll(
            int patientId, [FromQuery, Range(1, 1000000)] int pageNumber = 1,
            [FromQuery, Range(1, 100)] int pageSize = 20, CancellationToken ct = default)
        {
            var contacts = await _patientContactService.GetAllAsync(patientId, pageNumber, pageSize, ct);
            return SuccessResponse(SystemMessages.ContactsRetrieved, contacts);
        }

        // GET: api/patients/{patientId}/contacts/{contactId}
        [HttpGet("{contactId:int:min(1)}")]
        public async Task<ActionResult<ApiResponseModel<PatientContactResponse>>> GetById(int patientId, int contactId, CancellationToken ct)
        {
            var contactResponse = await _patientContactService.GetByIdAsync(patientId, contactId, ct);
            return SuccessResponse(SystemMessages.ContactRetrieved, contactResponse);
        }

        // POST: api/patients/{patientId}/contacts
        [HttpPost]
        public async Task<ActionResult<ApiResponseModel<PatientContactResponse>>> Create(int patientId, [FromBody] CreatePatientContactRequest request, CancellationToken ct)
        {
            var response = await _patientContactService.CreateAsync(patientId, request, ct);
            return CreatedResponse(nameof(GetById), new { patientId, contactId = response.ContactId }, SystemMessages.ContactCreated, response);
        }

        // PUT: api/patients/{patientId}/contacts/{contactId}
        [HttpPut("{contactId:int:min(1)}")]
        public async Task<ActionResult<ApiResponseModel<object?>>> Update(int patientId, int contactId, [FromBody] UpdatePatientContactRequest request, CancellationToken ct)
        {
            await _patientContactService.UpdateAsync(patientId, contactId, request, ct);
            return SuccessResponse(SystemMessages.ContactUpdated);
        }

        // DELETE: api/patients/{patientId}/contacts/{contactId}
        [HttpDelete("{contactId:int:min(1)}")]
        public async Task<ActionResult<ApiResponseModel<object?>>> Delete(int patientId, int contactId, CancellationToken ct)
        {
            await _patientContactService.DeleteAsync(patientId, contactId, ct);
            return SuccessResponse(SystemMessages.ContactDeleted);
        }
    }
}

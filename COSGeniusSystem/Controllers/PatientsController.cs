using COSGeniusSystem.Services;
using System.ComponentModel.DataAnnotations;
using COSGeniusSystem.Commons;
using COSGeniusSystem.DTOs;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;

namespace COSGeniusSystem.Controllers
{
    [Route("api/patients")]
    [Route("api/v1/patients")]
    [Authorize(Roles = RoleConstants.ReceptionStaff + "," + RoleConstants.Doctor)]
    public sealed class PatientsController : BaseApiController
    {
        private readonly IPatientService _patientService;

        public PatientsController(IPatientService patientService)
        {
            _patientService = patientService;
        }

        // GET: api/patients | api/v1/patients
        [HttpGet]
        public async Task<ActionResult<ApiResponseModel<PagedResult<PatientResponse>>>> GetAll(
            [FromQuery, Range(1, 1000000)] int pageNumber = 1,
            [FromQuery, Range(1, 100)] int pageSize = 10, CancellationToken ct = default,
            [FromQuery, StringLength(150)] string? search = null,
            [FromQuery] string? keyword = null,
            [FromQuery] string? code = null,
            [FromQuery] string? phone = null,
            [FromQuery] string? fullName = null,
            [FromQuery] bool mine = false)
        {
            var term = !string.IsNullOrWhiteSpace(keyword) ? keyword : search;
            int? doctorUserId = null;
            if (mine && User.IsInRole(RoleConstants.Doctor))
            {
                var val = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
                if (int.TryParse(val, out var uId)) doctorUserId = uId;
            }
            var patients = await _patientService.SearchAsync(term, pageNumber, pageSize, ct, code, phone, fullName, doctorUserId);
            return SuccessResponse(SystemMessages.PatientsRetrieved, patients);
        }

        [HttpGet("duplicate-phone")]
        [Authorize(Roles = RoleConstants.ReceptionStaff)]
        public async Task<ActionResult<ApiResponseModel<List<PatientResponse>>>> CheckDuplicatePhone(
            [FromQuery] string? phone, [FromQuery, Range(1, int.MaxValue)] int? excludePatientId, CancellationToken ct)
            => SuccessResponse(SystemMessages.PatientsRetrieved,
                await _patientService.CheckDuplicatePhoneAsync(phone, excludePatientId, ct));

        // GET: api/patients/mine | api/v1/patients/mine
        [HttpGet("mine")]
        [Authorize(Roles = RoleConstants.Doctor)]
        public async Task<ActionResult<ApiResponseModel<PagedResult<PatientResponse>>>> GetMine(
            [FromQuery, Range(1, 1000000)] int pageNumber = 1,
            [FromQuery, Range(1, 100)] int pageSize = 10, CancellationToken ct = default,
            [FromQuery, StringLength(150)] string? search = null,
            [FromQuery] string? keyword = null,
            [FromQuery] string? code = null,
            [FromQuery] string? phone = null,
            [FromQuery] string? fullName = null)
        {
            var term = !string.IsNullOrWhiteSpace(keyword) ? keyword : search;
            int? doctorUserId = null;
            var val = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
            if (int.TryParse(val, out var uId)) doctorUserId = uId;
            var patients = await _patientService.SearchAsync(term, pageNumber, pageSize, ct, code, phone, fullName, doctorUserId);
            return SuccessResponse(SystemMessages.PatientsRetrieved, patients);
        }

        // GET: api/patients/{patientId} | api/v1/patients/{patientId}
        [HttpGet("{patientId:int:min(1)}")]
        public async Task<ActionResult<ApiResponseModel<PatientResponse>>> GetById(int patientId, CancellationToken ct)
        {
            var patientResponse = await _patientService.GetByIdAsync(patientId, ct);
            return SuccessResponse(SystemMessages.PatientRetrieved, patientResponse);
        }

        // POST: api/patients | api/v1/patients
        [HttpPost]
        [Authorize(Roles = RoleConstants.ReceptionStaff)]
        public async Task<ActionResult<ApiResponseModel<PatientResponse>>> Create([FromBody] CreatePatientRequest request, CancellationToken ct)
        {
            var response = await _patientService.CreateAsync(request, ct);
            return CreatedResponse(nameof(GetById), new { patientId = response.PatientId }, SystemMessages.PatientCreated, response);
        }

        // PUT: api/patients/{patientId} | api/v1/patients/{patientId}
        [HttpPut("{patientId:int:min(1)}")]
        [HttpPatch("{patientId:int:min(1)}")]
        [Authorize(Roles = RoleConstants.ReceptionStaff)]
        public async Task<ActionResult<ApiResponseModel<object?>>> Update(int patientId, [FromBody] UpdatePatientRequest request, CancellationToken ct)
        {
            await _patientService.UpdateAsync(patientId, request, ct);
            return SuccessResponse(SystemMessages.PatientUpdated);
        }

        // DELETE: api/patients/{patientId} | api/v1/patients/{patientId}
        [HttpDelete("{patientId:int:min(1)}")]
        [Authorize(Roles = RoleConstants.ReceptionStaff)]
        public async Task<ActionResult<ApiResponseModel<object?>>> Delete(int patientId, CancellationToken ct)
        {
            await _patientService.DeleteAsync(patientId, ct);
            return SuccessResponse(SystemMessages.PatientDeleted);
        }
    }
}

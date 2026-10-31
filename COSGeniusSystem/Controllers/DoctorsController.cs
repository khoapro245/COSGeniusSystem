using COSGeniusSystem.Commons;
using COSGeniusSystem.Exceptions;
using COSGeniusSystem.DTOs.Doctors;
using COSGeniusSystem.Services.Doctors;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;

namespace COSGeniusSystem.Controllers;

[ApiController]
[Route("api/v1/doctors")]
public class DoctorsController : BaseApiController
{
    private readonly IDoctorService _doctorService;

    public DoctorsController(IDoctorService doctorService)
    {
        _doctorService = doctorService;
    }

    /// <summary>
    /// API-PHUC-01: Lấy danh sách bác sĩ/kỹ thuật viên.
    /// GET /api/v1/doctors
    /// </summary>
    [HttpGet]
    [Authorize(Roles = RoleConstants.ReceptionStaff + "," + RoleConstants.Doctor)]
    public async Task<ActionResult<ApiResponseModel<List<DoctorResponseDto>>>> GetDoctors([FromQuery] DoctorFilterQuery query, CancellationToken ct = default)
    {
        var result = await _doctorService.GetDoctorsAsync(query, ct);
        return SuccessResponse(SystemMessages.DoctorsRetrieved, result);
    }

    /// <summary>
    /// API-PHUC-02: Xem chi tiết bác sĩ.
    /// GET /api/v1/doctors/{id}
    /// </summary>
    [HttpGet("{id:int}")]
    public async Task<ActionResult<ApiResponseModel<DoctorDetailDto>>> GetDoctorById(int id)
    {
        var doctor = await _doctorService.GetDoctorByIdAsync(id);
        if (doctor is null)
            throw new NotFoundException($"Doctor with id {id} was not found.");

        return SuccessResponse(SystemMessages.DoctorRetrieved, doctor);
    }
}

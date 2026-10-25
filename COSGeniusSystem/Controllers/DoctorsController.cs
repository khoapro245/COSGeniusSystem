using COSGeniusSystem.DTOs.Doctors;
using COSGeniusSystem.Services.Doctors;
using Microsoft.AspNetCore.Mvc;

namespace COSGeniusSystem.Controllers;

[ApiController]
[Route("api/v1/doctors")]
public class DoctorsController : ControllerBase
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
    public async Task<ActionResult<List<DoctorResponseDto>>> GetDoctors([FromQuery] DoctorFilterQuery query)
    {
        var result = await _doctorService.GetDoctorsAsync(query);
        return Ok(result);
    }

    /// <summary>
    /// API-PHUC-02: Xem chi tiết bác sĩ.
    /// GET /api/v1/doctors/{id}
    /// </summary>
    [HttpGet("{id:int}")]
    public async Task<ActionResult<DoctorDetailDto>> GetDoctorById(int id)
    {
        var doctor = await _doctorService.GetDoctorByIdAsync(id);
        if (doctor is null)
            return NotFound(new { message = $"Doctor with id {id} was not found." });

        return Ok(doctor);
    }
}

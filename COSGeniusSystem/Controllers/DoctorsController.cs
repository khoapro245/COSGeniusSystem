using COSGeniusSystem.Commons;
using COSGeniusSystem.Exceptions;
using COSGeniusSystem.DTOs;
using COSGeniusSystem.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;

namespace COSGeniusSystem.Controllers;

[ApiController]
[Route("api/doctors")]
[Route("api/v1/doctors")]
public class DoctorsController : BaseApiController
{
    private readonly IDoctorService _doctorService;

    public DoctorsController(IDoctorService doctorService)
    {
        _doctorService = doctorService;
    }

    /// <summary>
    /// API-PHUC-00: Tổng quan danh sách bác sĩ & phòng khám, hàng đợi chờ khám cho Tiếp tân (Gộp thống kê & danh sách trong 1 endpoint).
    /// GET /api/v1/doctors/overview
    /// </summary>
    [HttpGet("overview")]
    [Authorize(Roles = RoleConstants.ReceptionStaff + "," + RoleConstants.Doctor)]
    public async Task<ActionResult<ApiResponseModel<DoctorClinicOverviewDto>>> GetDoctorClinicOverview(
        [FromQuery] DoctorClinicFilterQuery query,
        CancellationToken ct = default)
    {
        var result = await _doctorService.GetDoctorClinicOverviewAsync(query, ct);
        return SuccessResponse(SystemMessages.DoctorsRetrieved, result);
    }

    /// <summary>
    /// API-PHUC-01: Lấy danh sách bác sĩ/kỹ thuật viên.
    /// GET /api/v1/doctors
    /// </summary>
    [HttpGet]
    [Authorize(Roles = RoleConstants.ReceptionStaff + "," + RoleConstants.Doctor + "," + RoleConstants.Manager + "," + RoleConstants.Admin)]
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
    [Authorize(Roles = RoleConstants.ReceptionStaff + "," + RoleConstants.Doctor + "," + RoleConstants.Manager + "," + RoleConstants.Admin)]
    public async Task<ActionResult<ApiResponseModel<DoctorDetailDto>>> GetDoctorById(int id)
    {
        var doctor = await _doctorService.GetDoctorByIdAsync(id);
        if (doctor is null)
            throw new NotFoundException($"Doctor {id} was not found.");

        return SuccessResponse(SystemMessages.DoctorRetrieved, doctor);
    }

    /// <summary>
    /// API-DOCTOR-03: Đăng ký thông tin hồ sơ cho bác sĩ (Dành cho Quản lý / Admin).
    /// POST /api/v1/doctors
    /// </summary>
    [HttpPost]
    [Authorize(Roles = RoleConstants.Admin + "," + RoleConstants.Manager)]
    public async Task<ActionResult<ApiResponseModel<DoctorDetailDto>>> CreateDoctor(
        [FromBody] CreateDoctorRequestDto request,
        CancellationToken ct = default)
    {
        var result = await _doctorService.CreateDoctorAsync(request, ct);
        return CreatedResponse(
            nameof(GetDoctorById),
            new { id = result.DoctorId },
            new SystemMessage(201, "Hồ sơ bác sĩ đã được đăng ký thành công.", true),
            result);
    }
}

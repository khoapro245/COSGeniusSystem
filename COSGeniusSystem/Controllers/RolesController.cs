using COSGeniusSystem.Commons;
using COSGeniusSystem.DTOs;
using COSGeniusSystem.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace COSGeniusSystem.Controllers;

[ApiController]
[Route("api/v1/roles")]
public class RolesController : BaseApiController
{
    private readonly IUserService _userService;

    public RolesController(IUserService userService)
    {
        _userService = userService;
    }

    /// <summary>
    /// API-ROLE-01: Lấy danh sách 7 vai trò trong hệ thống.
    /// GET /api/v1/roles
    /// </summary>
    [HttpGet]
    [Authorize]
    public async Task<ActionResult<ApiResponseModel<List<RoleDto>>>> GetRoles(CancellationToken ct = default)
    {
        var roles = await _userService.GetRolesAsync(ct);
        return SuccessResponse(SystemMessages.RolesRetrieved, roles);
    }
}


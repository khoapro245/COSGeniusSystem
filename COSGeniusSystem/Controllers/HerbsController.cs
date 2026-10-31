using COSGeniusSystem.DTOs.Herbs;
using COSGeniusSystem.Commons;
using COSGeniusSystem.Exceptions;
using COSGeniusSystem.Services.Herbs;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;

namespace COSGeniusSystem.Controllers;

[ApiController]
[Route("api/v1/herbs")]
public class HerbsController : BaseApiController
{
    private readonly IHerbService _herbService;

    public HerbsController(IHerbService herbService)
    {
        _herbService = herbService;
    }

    /// <summary>
    /// API-TU-01: Lấy danh sách vị thuốc Đông y, hỗ trợ tìm kiếm và phân trang.
    /// GET /api/v1/herbs
    /// </summary>
    [HttpGet]
    [Authorize]
    public async Task<ActionResult<ApiResponseModel<List<HerbResponseDto>>>> GetHerbs([FromQuery] HerbFilterQuery query, CancellationToken ct = default)
    {
        var result = await _herbService.GetHerbsAsync(query, ct);
        return SuccessResponse(SystemMessages.HerbsRetrieved, result);
    }

    /// <summary>
    /// API-TU-02: Xem thông tin chi tiết vị thuốc.
    /// GET /api/v1/herbs/{id}
    /// </summary>
    [HttpGet("{id:int}")]
    [Authorize]
    public async Task<ActionResult<ApiResponseModel<HerbDetailDto>>> GetHerbById(int id)
    {
        var herb = await _herbService.GetHerbByIdAsync(id);
        if (herb is null)
            throw new NotFoundException($"Herb with id {id} was not found.");

        return SuccessResponse(SystemMessages.HerbRetrieved, herb);
    }

    /// <summary>
    /// API-TU-03: Thêm mới vị thuốc vào danh mục.
    /// POST /api/v1/herbs
    /// </summary>
    [HttpPost]
    [Authorize(Roles = RoleConstants.Admin + "," + RoleConstants.Manager)]
    public async Task<ActionResult<ApiResponseModel<HerbResponseDto>>> CreateHerb(
        [FromBody] CreateHerbRequest request)
    {
        var (dto, error) = await _herbService.CreateHerbAsync(request);

        if (error is not null)
            throw new BadRequestException(error);

        return CreatedResponse(
            nameof(GetHerbById),
            new { id = dto!.HerbId },
            SystemMessages.HerbCreated,
            dto);
    }

    /// <summary>
    /// API-TU-04: Cập nhật thông tin vị thuốc.
    /// PUT /api/v1/herbs/{id}
    /// </summary>
    [HttpPut("{id:int}")]
    [Authorize(Roles = RoleConstants.Admin + "," + RoleConstants.Manager)]
    public async Task<ActionResult<ApiResponseModel<HerbResponseDto>>> UpdateHerb(
        int id,
        [FromBody] UpdateHerbRequest request)
    {
        var (dto, error) = await _herbService.UpdateHerbAsync(id, request);

        // dto == null && error == null → không tìm thấy → 404
        if (dto is null && error is null)
            throw new NotFoundException($"Herb with id {id} was not found.");

        // dto == null && error != null → lỗi validation → 400
        if (error is not null)
            throw new BadRequestException(error);

        return SuccessResponse(SystemMessages.HerbUpdated, dto!);
    }

    /// <summary>
    /// API-TU-05: Xem tồn kho thực tế theo lô (FEFO), kèm tổng tồn kho.
    /// GET /api/v1/herbs/{id}/batches
    /// </summary>
    [HttpGet("{id:int}/batches")]
    [Authorize]
    public async Task<ActionResult<ApiResponseModel<HerbBatchesResponseDto>>> GetHerbBatches(int id)
    {
        var result = await _herbService.GetHerbBatchesAsync(id);
        if (result is null)
            throw new NotFoundException($"Herb with id {id} was not found.");

        return SuccessResponse(SystemMessages.HerbsRetrieved, result);
    }

    /// <summary>
    /// Xóa hoặc ngưng hoạt động vị thuốc khỏi danh mục.
    /// DELETE /api/v1/herbs/{id}
    /// </summary>
    [HttpDelete("{id:int}")]
    [Authorize(Roles = RoleConstants.Admin + "," + RoleConstants.Manager)]
    public async Task<ActionResult<ApiResponseModel<object?>>> DeleteHerb(int id, CancellationToken ct = default)
    {
        var (success, error) = await _herbService.DeleteHerbAsync(id, ct);
        if (!success && error is null)
            throw new NotFoundException($"Herb with id {id} was not found.");

        if (!success)
            throw new BadRequestException(error!);

        return SuccessResponse(SystemMessages.HerbDeleted);
    }
}

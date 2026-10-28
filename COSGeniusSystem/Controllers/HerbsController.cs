using COSGeniusSystem.DTOs.Herbs;
using COSGeniusSystem.Services.Herbs;
using Microsoft.AspNetCore.Mvc;

namespace COSGeniusSystem.Controllers;

[ApiController]
[Route("api/v1/herbs")]
public class HerbsController : ControllerBase
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
    public async Task<ActionResult<List<HerbResponseDto>>> GetHerbs([FromQuery] HerbFilterQuery query)
    {
        var result = await _herbService.GetHerbsAsync(query);
        return Ok(result);
    }

    /// <summary>
    /// API-TU-02: Xem thông tin chi tiết vị thuốc.
    /// GET /api/v1/herbs/{id}
    /// </summary>
    [HttpGet("{id:int}")]
    public async Task<ActionResult<HerbDetailDto>> GetHerbById(int id)
    {
        var herb = await _herbService.GetHerbByIdAsync(id);
        if (herb is null)
            return NotFound(new { message = $"Herb with id {id} was not found." });

        return Ok(herb);
    }

    /// <summary>
    /// API-TU-03: Thêm mới vị thuốc vào danh mục.
    /// POST /api/v1/herbs
    /// </summary>
    [HttpPost]
    public async Task<ActionResult<HerbResponseDto>> CreateHerb(
        [FromBody] CreateHerbRequest request)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        var (dto, error) = await _herbService.CreateHerbAsync(request);

        if (error is not null)
            return BadRequest(new { message = error });

        return CreatedAtAction(
            nameof(GetHerbById),
            new { id = dto!.HerbId },
            dto);
    }

    /// <summary>
    /// API-TU-04: Cập nhật thông tin vị thuốc.
    /// PUT /api/v1/herbs/{id}
    /// </summary>
    [HttpPut("{id:int}")]
    public async Task<ActionResult<HerbResponseDto>> UpdateHerb(
        int id,
        [FromBody] UpdateHerbRequest request)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        var (dto, error) = await _herbService.UpdateHerbAsync(id, request);

        // dto == null && error == null → không tìm thấy → 404
        if (dto is null && error is null)
            return NotFound(new { message = $"Herb with id {id} was not found." });

        // dto == null && error != null → lỗi validation → 400
        if (error is not null)
            return BadRequest(new { message = error });

        return Ok(dto);
    }

    /// <summary>
    /// API-TU-05: Xem tồn kho thực tế theo lô (FEFO), kèm tổng tồn kho.
    /// GET /api/v1/herbs/{id}/batches
    /// </summary>
    [HttpGet("{id:int}/batches")]
    public async Task<ActionResult<HerbBatchesResponseDto>> GetHerbBatches(int id)
    {
        var result = await _herbService.GetHerbBatchesAsync(id);
        if (result is null)
            return NotFound(new { message = $"Herb with id {id} was not found." });

        return Ok(result);
    }
}

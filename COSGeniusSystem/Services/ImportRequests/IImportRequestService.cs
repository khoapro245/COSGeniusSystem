// [hieppm] Start: Interface cho Service xử lý Yêu cầu bổ sung kho (ImportRequest)
using COSGeniusSystem.DTOs;

namespace COSGeniusSystem.Services;

public interface IImportRequestService
{
    Task<List<ImportRequestDto>> GetImportRequestsAsync(string? status = null, CancellationToken ct = default);
    Task<ImportRequestDto?> GetImportRequestDetailAsync(string idOrCode, CancellationToken ct = default);
    Task<ImportRequestDto> CreateImportRequestAsync(int userId, CreateImportRequestRequest request, CancellationToken ct = default);
    Task<ImportRequestDto> ReviewImportRequestAsync(int managerId, int requestId, ReviewImportRequestRequest request, CancellationToken ct = default);
}
// [hieppm] End: Interface cho Service xử lý Yêu cầu bổ sung kho

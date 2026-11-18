using COSGeniusSystem.DTOs;

namespace COSGeniusSystem.Services;

public interface IDisposalService
{
    Task<List<DisposalDto>> GetDisposalsAsync(CancellationToken ct = default);
    Task<DisposalDto?> GetDisposalByIdOrCodeAsync(string idOrCode, CancellationToken ct = default);
    Task<DisposalDto> CreateDisposalAsync(CreateDisposalRequest request, CancellationToken ct = default);
    Task<DisposalDto> ApproveDisposalAsync(string idOrCode, ApproveDisposalRequest request, CancellationToken ct = default);
    Task<DisposalDto> RejectDisposalAsync(string idOrCode, string? reason, CancellationToken ct = default);
    Task<DisposalDto> ExecuteDisposalAsync(string idOrCode, ExecuteDisposalRequest request, CancellationToken ct = default);
}


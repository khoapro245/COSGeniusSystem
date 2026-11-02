using System.Security.Claims;
using COSGeniusSystem.DTOs;

namespace COSGeniusSystem.Services;

public interface ITreatmentProtocolService
{
    Task<List<TreatmentQueueDto>> GetQueueAsync(ClaimsPrincipal actor, TreatmentQueueQuery query, CancellationToken ct = default);
    Task<TreatmentQueueDto> CreateTreatmentRequestAsync(ClaimsPrincipal actor, CreateTreatmentRequest request, CancellationToken ct = default);
}

/// <summary>
/// Alias interface kế thừa từ ITreatmentProtocolService để phân biệt rõ nghiệp vụ hàng đợi trị liệu
/// </summary>
public interface ITreatmentQueueService : ITreatmentProtocolService
{
}


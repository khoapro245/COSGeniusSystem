using COSGeniusSystem.DTOs.Treatments;

namespace COSGeniusSystem.Services.Treatments;

public interface ITreatmentProtocolService
{
    Task<List<TreatmentQueueDto>> GetQueueAsync(TreatmentQueueQuery query, CancellationToken ct = default);
    Task<TreatmentQueueDto> CreateTreatmentRequestAsync(CreateTreatmentRequest request, CancellationToken ct = default);
}

/// <summary>
/// Alias interface kế thừa từ ITreatmentProtocolService để phân biệt rõ nghiệp vụ hàng đợi trị liệu
/// </summary>
public interface ITreatmentQueueService : ITreatmentProtocolService
{
}


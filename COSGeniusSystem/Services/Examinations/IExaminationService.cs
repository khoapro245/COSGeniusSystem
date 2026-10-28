using COSGeniusSystem.DTOs.Examinations;

namespace COSGeniusSystem.Services.Examinations;

public interface IExaminationService
{
    Task<List<ExaminationVisitDto>> GetVisitsAsync(ExaminationFilterQuery query, CancellationToken ct = default);
    Task<ExaminationVisitDto> CreateVisitAsync(CreateExaminationVisitRequest request, CancellationToken ct = default);
    Task<ExaminationVisitDto> GetVisitByIdAsync(int examSheetId, CancellationToken ct = default);
    Task CancelVisitAsync(int examSheetId, CancellationToken ct = default);
}

/// <summary>
/// Alias interface kế thừa từ IExaminationService để mở rộng linh hoạt theo quy chuẩn kiến trúc
/// </summary>
public interface IExamSheetService : IExaminationService
{
}


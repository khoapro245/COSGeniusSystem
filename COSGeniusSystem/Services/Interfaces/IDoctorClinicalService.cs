using COSGeniusSystem.DTOs.DoctorClinical;

namespace COSGeniusSystem.Services.Interfaces;

public interface IDoctorClinicalService
{
    /// <summary>
    /// 1. Tra cứu danh sách đăng ký khám và hàng đợi khám bệnh của bác sĩ (DOC-01 & DOC-06 Cột trái).
    /// </summary>
    Task<DoctorExamQueueResponseDto> GetExamQueueAsync(DoctorExamFilterDto filter, CancellationToken ct = default);

    /// <summary>
    /// 2. Nghiệp vụ "Gọi vào khám": chuyển trạng thái Chờ khám -> Đang khám, cập nhật thời gian bắt đầu khám.
    /// </summary>
    Task<MedicalRecordWorkspaceDto> CallPatientAsync(int examSheetId, CancellationToken ct = default);

    /// <summary>
    /// 3. Lấy chi tiết toàn bộ hồ sơ khám bệnh workspace (DOC-02, DOC-05, DOC-06).
    /// </summary>
    Task<MedicalRecordWorkspaceDto> GetMedicalRecordWorkspaceAsync(int examSheetId, CancellationToken ct = default);

    /// <summary>
    /// 4. Lưu thông tin khám lâm sàng, sinh hiệu, tứ chẩn YHCT và chẩn đoán.
    /// </summary>
    Task<MedicalRecordWorkspaceDto> SaveClinicalExamAsync(int examSheetId, SaveClinicalExamRequestDto request, CancellationToken ct = default);

    /// <summary>
    /// 5. Hoàn tất lượt khám bệnh và khóa hồ sơ bệnh án (IsReadOnly = true).
    /// </summary>
    Task<MedicalRecordWorkspaceDto> CompleteExaminationAsync(int examSheetId, SaveClinicalExamRequestDto? request, CancellationToken ct = default);

    /// <summary>
    /// 6. Kê dịch vụ điều trị (đơn lẻ hoặc theo liệu trình) tại Tab Kê dịch vụ (DOC-03).
    /// </summary>
    Task<ServiceIndicationDto> SaveServiceIndicationAsync(int examSheetId, SaveServiceIndicationRequestDto request, CancellationToken ct = default);

    /// <summary>
    /// 7. Danh mục bài thuốc mẫu và danh sách vị dược liệu phục vụ Tab Kê đơn thuốc (DOC-04).
    /// </summary>
    Task<PrescriptionCatalogDto> GetPrescriptionCatalogAsync(CancellationToken ct = default);

    /// <summary>
    /// 8. Kê đơn thuốc (Lưu nháp hoặc Gửi Herbalist). Sau khi gửi Herbalist, đơn bị khóa không cho sửa.
    /// </summary>
    Task<PrescriptionDetailDto> SavePrescriptionAsync(int examSheetId, SavePrescriptionRequestDto request, CancellationToken ct = default);
}


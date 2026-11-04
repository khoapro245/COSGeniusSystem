using System.Text.Json;
using System.Text.Json.Serialization;
using COSGeniusSystem.Models;

namespace COSGeniusSystem.DTOs.DoctorClinical;

public class ClinicalMetadataJson
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    public string? QueueNumber { get; set; }
    public bool IsPriority { get; set; }
    public string Status { get; set; } = "Chờ khám"; // "Chờ khám", "Đang khám", "Hoàn tất"
    public DateTime? ExamStartTime { get; set; }

    // Sinh hiệu
    public int? PulseRate { get; set; }
    public decimal? Temperature { get; set; }
    public int? RespiratoryRate { get; set; }
    public decimal? Height { get; set; }
    public decimal? Weight { get; set; }
    public decimal? SpO2 { get; set; }

    // Tứ chẩn YHCT
    public string? VongChan { get; set; }
    public string? VanChan_NgheNgui { get; set; }
    public string? VanChan_Hoi { get; set; }
    public string? ThietChan { get; set; }
    public string? FourExamsSummary { get; set; }
    public string? BienChung { get; set; }
    public string? TraditionalDiagnosis { get; set; }

    // Chẩn đoán YHHD
    public string? Symptoms { get; set; }
    public string? PreliminaryDiagnosis { get; set; }
    public string? IcdCode { get; set; }
    public string? IcdName { get; set; }

    // Kê dịch vụ
    public string? TreatmentType { get; set; }
    public int TotalSessions { get; set; }
    public string? IndicationNote { get; set; }
    public string? AdditionalContent { get; set; }

    // Kê đơn thuốc
    public string? PrescriptionStatus { get; set; } = "Nháp"; // "Nháp", "Đã gửi Herbalist"
    public string? PrescriptionMethod { get; set; }
    public int? TemplateFormulaId { get; set; }
    public string? PrescriptionNote { get; set; }
    public int? TotalDoses { get; set; }
    public string? PrescriptionCode { get; set; }

    public static ClinicalMetadataJson FromMedicalRecord(MedicalRecord mr)
    {
        ClinicalMetadataJson? meta = null;
        if (!string.IsNullOrWhiteSpace(mr.DoctorAdvice) && mr.DoctorAdvice.TrimStart().StartsWith("{"))
        {
            try
            {
                meta = JsonSerializer.Deserialize<ClinicalMetadataJson>(mr.DoctorAdvice, JsonOptions);
            }
            catch
            {
                meta = null;
            }
        }

        meta ??= new ClinicalMetadataJson();

        // Đồng bộ dữ liệu từ các cột bảng nếu metadata chưa có
        if (string.IsNullOrWhiteSpace(meta.Symptoms) && !string.IsNullOrWhiteSpace(mr.Symptoms))
            meta.Symptoms = mr.Symptoms;
        if (string.IsNullOrWhiteSpace(meta.VongChan) && !string.IsNullOrWhiteSpace(mr.Inspection))
            meta.VongChan = mr.Inspection;
        if (string.IsNullOrWhiteSpace(meta.ThietChan) && !string.IsNullOrWhiteSpace(mr.Palpation))
            meta.ThietChan = mr.Palpation;
        if (string.IsNullOrWhiteSpace(meta.TraditionalDiagnosis) && !string.IsNullOrWhiteSpace(mr.TraditionalDiagnosis))
            meta.TraditionalDiagnosis = mr.TraditionalDiagnosis;

        if (!meta.IsPriority && mr.Symptoms != null && mr.Symptoms.Contains("[Ưu tiên]"))
            meta.IsPriority = true;

        if (string.IsNullOrWhiteSpace(meta.Status) || meta.Status == "Chờ khám")
        {
            if (mr.Status == "Hoàn tất" || mr.Status == "Đã hoàn tất" || mr.Status == "COMPLETED")
                meta.Status = "Hoàn tất";
            else if (mr.Status == "Đang khám" || mr.Status == "IN_PROGRESS")
                meta.Status = "Đang khám";
            else if (!string.IsNullOrWhiteSpace(mr.Status) && mr.Status != "Chờ khám" && mr.Status != "WAITING")
                meta.Status = "Hoàn tất";
        }

        return meta;
    }

    public string ToJson()
    {
        return JsonSerializer.Serialize(this, JsonOptions);
    }
}

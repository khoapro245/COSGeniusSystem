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

    public string Status { get; set; } = "Chờ khám"; // "Chờ khám", "Đang khám", "Hoàn tất"
    public DateTime? ExamStartTime { get; set; }
    public DateTime? CompletedAt { get; set; }
    public bool IsPriority { get; set; }
    public string? QueueNumber { get; set; }

    // Sinh hiệu (Vital Signs)
    public int? PulseRate { get; set; }
    public decimal? Temperature { get; set; }
    public int? RespiratoryRate { get; set; }
    public decimal? Height { get; set; }
    public decimal? Weight { get; set; }
    public decimal? SpO2 { get; set; }

    // Triệu chứng & Chẩn đoán
    public string? Symptoms { get; set; }
    public string? PreliminaryDiagnosis { get; set; }
    public string? IcdCode { get; set; }
    public string? IcdName { get; set; }

    // Y học cổ truyền (Tứ chẩn)
    public string? VongChan { get; set; }
    public string? VanChan_NgheNgui { get; set; }
    public string? VanChan_Hoi { get; set; }
    public string? ThietChan { get; set; }
    public string? FourExamsSummary { get; set; }
    public string? BienChung { get; set; }
    public string? TraditionalDiagnosis { get; set; }

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

    public static ClinicalMetadataJson FromExamSheet(ExamSheet es)
    {
        ClinicalMetadataJson? meta = null;
        if (!string.IsNullOrWhiteSpace(es.DoctorAdvice) && es.DoctorAdvice.TrimStart().StartsWith("{"))
        {
            try
            {
                meta = JsonSerializer.Deserialize<ClinicalMetadataJson>(es.DoctorAdvice, JsonOptions);
            }
            catch
            {
                meta = null;
            }
        }

        meta ??= new ClinicalMetadataJson();

        // Đồng bộ dữ liệu từ các cột bảng nếu metadata chưa có
        if (string.IsNullOrWhiteSpace(meta.Symptoms) && !string.IsNullOrWhiteSpace(es.Symptoms))
            meta.Symptoms = es.Symptoms;
        if (string.IsNullOrWhiteSpace(meta.VongChan) && !string.IsNullOrWhiteSpace(es.TongueDiagnosis))
            meta.VongChan = es.TongueDiagnosis;
        if (string.IsNullOrWhiteSpace(meta.ThietChan) && !string.IsNullOrWhiteSpace(es.PulseDiagnosis))
            meta.ThietChan = es.PulseDiagnosis;
        if (string.IsNullOrWhiteSpace(meta.TraditionalDiagnosis) && !string.IsNullOrWhiteSpace(es.FinalDiagnosis))
            meta.TraditionalDiagnosis = es.FinalDiagnosis;

        if (!meta.IsPriority && es.Symptoms != null && es.Symptoms.Contains("[Ưu tiên]"))
            meta.IsPriority = true;

        if (string.IsNullOrWhiteSpace(meta.Status) || meta.Status == "Chờ khám")
        {
            if (es.FinalDiagnosis == "Hoàn tất" || es.FinalDiagnosis == "Đã hoàn tất" || es.ExaminationStatus == "COMPLETED")
                meta.Status = "Hoàn tất";
            else if (es.FinalDiagnosis == "Đang khám" || es.ExaminationStatus == "IN_PROGRESS")
                meta.Status = "Đang khám";
            else if (!string.IsNullOrWhiteSpace(es.FinalDiagnosis) && es.FinalDiagnosis != "Chờ khám")
                meta.Status = "Hoàn tất";
        }

        return meta;
    }

    public string ToJson() => JsonSerializer.Serialize(this, JsonOptions);
}


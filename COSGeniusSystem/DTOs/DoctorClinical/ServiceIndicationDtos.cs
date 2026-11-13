namespace COSGeniusSystem.DTOs.DoctorClinical;

public class ServiceIndicationDto
{
    public string TreatmentType { get; set; } = "Theo liệu trình"; // "Theo liệu trình" hoặc "Đơn lẻ"
    public int TotalSessions { get; set; }
    public string IndicationNote { get; set; } = string.Empty;
    public string AdditionalContent { get; set; } = string.Empty;
    public List<IndicatedServiceItemDto> Services { get; set; } = new();
}

public class IndicatedServiceItemDto
{
    public int ServiceId { get; set; }
    public string ServiceCode { get; set; } = string.Empty;
    public string ServiceName { get; set; } = string.Empty;
    public string TreatmentType { get; set; } = string.Empty;
    public string Note { get; set; } = string.Empty;
    public decimal Price { get; set; }
    public string Unit { get; set; } = string.Empty;
}

public class SaveServiceIndicationRequestDto
{
    public string TreatmentType { get; set; } = "Theo liệu trình";
    public int TotalSessions { get; set; } = 1;
    public string? IndicationNote { get; set; }
    public string? AdditionalContent { get; set; }
    public List<SaveServiceItemDto> Services { get; set; } = new();
}

public class SaveServiceItemDto
{
    public int ServiceId { get; set; }
    public string? Note { get; set; }
}


namespace COSGeniusSystem.DTOs.Treatments;

public sealed class TreatmentQueueDto
{
    public int Stt { get; init; }
    public int UsageId { get; init; }
    public string PatientName { get; init; } = null!;
    public string? PatientCode { get; init; }
    public string? Phone { get; init; }
    public string YcdtCode { get; init; } = null!;
    public DateTime ReceptionTime { get; init; }
    public string? ProtocolName { get; init; }
    public string? SessionInfo { get; init; }
    public string? DoctorName { get; init; }
    public string Status { get; init; } = "Chờ điều trị";
}

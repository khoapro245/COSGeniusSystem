namespace COSGeniusSystem.DTOs;

public sealed class TreatmentQueueDto
{
    public int Stt { get; init; }
    public int PlanId { get; init; }
    public byte[] RowVersion { get; init; } = [];
    public int SessionId { get; init; }
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

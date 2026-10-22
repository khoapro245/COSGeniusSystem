using System;
using System.Collections.Generic;

namespace COSGeniusSystem.Models;

public partial class TreatmentProtocol
{
    public int ProtocolId { get; set; }

    public int RecordId { get; set; }

    public int DoctorId { get; set; }

    public string ProtocolName { get; set; } = null!;

    public DateOnly StartDate { get; set; }

    public DateOnly? EndDate { get; set; }

    public string? Description { get; set; }

    public virtual Doctor Doctor { get; set; } = null!;

    public virtual MedicalRecord Record { get; set; } = null!;
}

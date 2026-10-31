using System;
using System.Collections.Generic;

namespace COSGeniusSystem.Models;

public partial class ExaminationRegistration
{
    public int RegistrationId { get; set; }

    public string? RegistrationCode { get; set; }

    public int PatientId { get; set; }

    public int DoctorId { get; set; }

    public int RegisteredBy { get; set; }

    public DateTime RegisteredAt { get; set; }

    public DateOnly BusinessDate { get; set; }

    public int QueueNumber { get; set; }

    public bool IsPriority { get; set; }

    public int? ContactId { get; set; }

    public string? Symptoms { get; set; }

    public string? Note { get; set; }

    public string Status { get; set; } = null!;

    public DateTime? StartedAt { get; set; }

    public DateTime? CompletedAt { get; set; }

    public DateTime? CancelledAt { get; set; }

    public DateTime? UpdatedAt { get; set; }

    public byte[] RowVersion { get; set; } = new byte[8];

    public virtual PatientContact? Contact { get; set; }

    public virtual Doctor Doctor { get; set; } = null!;

    public virtual MedicalRecord? MedicalRecord { get; set; }

    public virtual Patient Patient { get; set; } = null!;

    public virtual User RegisteredByNavigation { get; set; } = null!;
}

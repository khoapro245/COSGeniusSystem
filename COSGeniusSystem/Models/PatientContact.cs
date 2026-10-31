using System;
using System.Collections.Generic;

namespace COSGeniusSystem.Models;

public partial class PatientContact
{
    public int ContactId { get; set; }

    public int PatientId { get; set; }

    public string FullName { get; set; } = null!;

    public string? Relationship { get; set; }

    public string? Phone { get; set; }

    public virtual ICollection<ExaminationRegistration> ExaminationRegistrations { get; set; } = new List<ExaminationRegistration>();

    public virtual Patient Patient { get; set; } = null!;
}

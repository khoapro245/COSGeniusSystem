using System;
using System.Collections.Generic;

namespace COSGeniusSystem.Models;

public partial class Patient
{
    public int PatientId { get; set; }

    public string FullName { get; set; } = null!;

    public string? Gender { get; set; }

    public DateOnly? DateOfBirth { get; set; }

    public string? Phone { get; set; }

    public string? Address { get; set; }

    public string? IdentityCard { get; set; }

    public DateTime CreatedAt { get; set; }

    public string? PatientCode { get; set; }

    public string? Email { get; set; }

    public DateOnly? IdentityCardIssuedDate { get; set; }

    public string? IdentityCardIssuedPlace { get; set; }

    public string? PhotoUrl { get; set; }

    public string? Province { get; set; }

    public string? Ward { get; set; }

    public string? Occupation { get; set; }

    public string? Workplace { get; set; }

    public string? Nationality { get; set; }

    public string? Ethnicity { get; set; }

    public string? AllergyNote { get; set; }

    public virtual ICollection<ExaminationRegistration> ExaminationRegistrations { get; set; } = new List<ExaminationRegistration>();

    public virtual ICollection<MedicalRecord> MedicalRecords { get; set; } = new List<MedicalRecord>();

    public virtual ICollection<PatientContact> PatientContacts { get; set; } = new List<PatientContact>();
}

using System;
using System.Collections.Generic;

namespace COSGeniusSystem.Models;

public partial class HerbalPrescription
{
    public int PrescriptionId { get; set; }

    public string? PrescriptionCode { get; set; }

    public int RecordId { get; set; }

    public int? SourceHerbalFormulaId { get; set; }

    public string? PrescriptionName { get; set; }

    public int TotalDoses { get; set; }

    public int? DosageDays { get; set; }

    public string? UsageInstructions { get; set; }

    public string? Note { get; set; }

    public string Status { get; set; } = null!;

    public int CreatedBy { get; set; }

    public DateTime? SentAt { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime? UpdatedAt { get; set; }

    public byte[] RowVersion { get; set; } = new byte[8];

    public virtual User CreatedByNavigation { get; set; } = null!;

    public virtual ICollection<HerbalPrescriptionDetail> HerbalPrescriptionDetails { get; set; } = new List<HerbalPrescriptionDetail>();

    public virtual ICollection<InvoiceDetail> InvoiceDetails { get; set; } = new List<InvoiceDetail>();

    public virtual MedicineDispense? MedicineDispense { get; set; }

    public virtual MedicalRecord Record { get; set; } = null!;

    public virtual HerbalFormula? SourceHerbalFormula { get; set; }
}

using COSGeniusSystem.Data;
using COSGeniusSystem.DTOs;
using COSGeniusSystem.Models;
using COSGeniusSystem.Exceptions;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;
namespace COSGeniusSystem.Services;

// Called only within the caller's business transaction, after clinical changes are saved.
// Each accrual creates a new invoice: no previously reviewed or paid invoice is amended.
public static class InvoiceAccrual
{
    public static bool IsPaid(string status) => status is "PAID" or "Đã thanh toán";
    public static async Task GenerateAsync(ApplicationDbContext db, int recordId, CancellationToken ct = default)
    {
        if (db.Database.IsRelational() && db.Database.CurrentTransaction == null)
            throw new InvalidOperationException("Invoice accrual requires a business transaction.");
        var record = db.Database.IsSqlServer()
            ? await db.MedicalRecords.FromSqlInterpolated($"SELECT * FROM dbo.MedicalRecord WITH (UPDLOCK,HOLDLOCK) WHERE RecordID={recordId}").SingleAsync(ct)
            : await db.MedicalRecords.SingleAsync(x => x.RecordId == recordId, ct);
        await db.Entry(record).Reference(x => x.Patient).LoadAsync(ct);
        await db.Entry(record).Reference(x => x.Doctor).LoadAsync(ct);
        var existing = await db.InvoiceDetails.Include(x => x.Invoice).Where(x => x.Invoice.RecordId == recordId
            && x.Invoice.PaymentStatus != "CANCELLED" && x.Invoice.PaymentStatus != "Đã hủy").ToListAsync(ct);
        var lines = new List<InvoiceDetail>();
        var prescriptions = await db.HerbalPrescriptions.Include(x => x.HerbalPrescriptionDetails).ThenInclude(x => x.Herb)
            .Where(x => x.RecordId == recordId && x.MedicineDispense != null && x.MedicineDispense.Status == "COMPLETED").ToListAsync(ct);
        foreach (var rx in prescriptions.Where(x => !existing.Any(i => i.PrescriptionId == x.PrescriptionId)))
        {
            var ingredients = rx.HerbalPrescriptionDetails.Select(d => new InvoiceIngredient(d.HerbNameSnapshot,
                d.DosagePerDose, rx.TotalDoses, d.DosagePerDose * rx.TotalDoses, "g", (d.Herb.Unit == "kg" ? d.Herb.StandardPrice / 1000m : d.Herb.StandardPrice),
                decimal.Round(d.DosagePerDose * rx.TotalDoses * (d.Herb.Unit == "kg" ? d.Herb.StandardPrice / 1000m : d.Herb.StandardPrice), 2, MidpointRounding.AwayFromZero))).ToList();
            // Canonical dispensing quantities are grams. Reject ambiguous catalog units rather than misprice them.
            if (rx.TotalDoses <= 0 || ingredients.Count == 0 || rx.HerbalPrescriptionDetails.Any(d => d.DosagePerDose <= 0
                || d.Herb.StandardPrice < 0 || !Herbalist.HerbalistWorkflowRules.SupportsStockUnit(d.Herb.Unit)))
                throw new ConflictException("Cần chuẩn hóa giá bán theo gram và thành phần đơn thuốc trước khi lập hóa đơn.");
            lines.Add(new InvoiceDetail { ItemType = "HERB_REMEDY", PrescriptionId = rx.PrescriptionId,
                SourceKey = $"RX:{rx.PrescriptionId}", ItemName = rx.PrescriptionName ?? rx.PrescriptionCode ?? "Đơn thuốc",
                Quantity = 1, UnitPrice = ingredients.Sum(i => i.SubTotal), UnitSnapshot = "đơn",
                IngredientsJson = JsonSerializer.Serialize(ingredients) });
        }
        var details = await db.TreatmentPlanDetails.Include(x => x.Plan).Include(x => x.UsageHistories).ThenInclude(x => x.Session)
            .Where(x => x.Plan.RecordId == recordId).ToListAsync(ct);
        foreach (var d in details)
        {
            var course = d.Plan.TreatmentType == "COURSE";
            var eligible = course
                ? (record.Status == "COMPLETED" && d.Plan.ConfirmedAt.HasValue && d.Plan.Status is "CONFIRMED" or "IN_PROGRESS" or "COMPLETED" ? d.PlannedQuantity : 0)
                : d.Plan.TreatmentType == "SINGLE" ? d.UsageHistories.Where(u => u.Session.Status == "COMPLETED").Sum(u => u.Quantity) : 0;
            var covered = existing.Where(i => i.PlanDetailId == d.PlanDetailId).Sum(i => i.Quantity);
            var quantity = eligible - covered;
            if (quantity <= 0) continue;
            if (d.UnitPrice < 0) throw new ConflictException("Giá dịch vụ không hợp lệ.");
            lines.Add(new InvoiceDetail { ItemType = "SERVICE", PlanDetailId = d.PlanDetailId,
                SourceKey = $"SERVICE:{d.PlanDetailId}:{eligible}", ItemName = d.ServiceNameSnapshot,
                Quantity = quantity, UnitPrice = d.UnitPrice, UnitSnapshot = d.UnitSnapshot, TreatmentTypeSnapshot = d.Plan.TreatmentType });
        }
        if (lines.Count == 0) return;
        var cancelled = await db.InvoiceDetails.AsNoTracking().Where(x => x.Invoice.RecordId == recordId
            && (x.Invoice.PaymentStatus == "CANCELLED" || x.Invoice.PaymentStatus == "Đã hủy"))
            .OrderByDescending(x => x.InvoiceId).ThenByDescending(x => x.InvoiceDetailId).ToListAsync(ct);
        var pricedLines = new List<InvoiceDetail>();
        foreach (var line in lines)
        {
            // Keep each cancelled debt at its own snapshot price even when more SINGLE
            // usage became eligible in the meantime. A cumulative quantity must not reprice it.
            var remaining = line.Quantity;
            foreach (var previous in cancelled.Where(x => x.ItemType == line.ItemType && x.PrescriptionId == line.PrescriptionId
                && x.PlanDetailId == line.PlanDetailId && x.Quantity > 0))
            {
                var key = previous.SourceKey ?? $"LEGACY:{previous.InvoiceDetailId}";
                if (existing.Any(e => e.SourceKey == key) || pricedLines.Any(e => e.SourceKey == key)) continue;
                if (previous.Quantity > remaining) continue;
                pricedLines.Add(new InvoiceDetail { ItemType = line.ItemType, PrescriptionId = line.PrescriptionId,
                    PlanDetailId = line.PlanDetailId, SourceKey = key, Quantity = previous.Quantity,
                    ItemName = previous.ItemName, UnitPrice = previous.UnitPrice, IngredientsJson = previous.IngredientsJson,
                    UnitSnapshot = previous.UnitSnapshot, TreatmentTypeSnapshot = previous.TreatmentTypeSnapshot });
                remaining -= previous.Quantity;
                if (remaining == 0) break;
            }
            if (remaining > 0)
            {
                line.Quantity = remaining;
                if (pricedLines.Any(x => x.SourceKey == line.SourceKey)) line.SourceKey = $"ACCRUAL:{Guid.NewGuid():N}";
                pricedLines.Add(line);
            }
        }
        lines = pricedLines;
        var total = lines.Sum(x => x.Quantity * x.UnitPrice);
        db.Invoices.Add(new Invoice { RecordId = recordId, IssuedDate = DateTime.UtcNow, PaymentStatus = "UNPAID",
            PaymentMethod = "", PatientNameSnapshot = record.Patient.FullName, PatientCodeSnapshot = record.Patient.PatientCode,
            PhoneSnapshot = record.Patient.Phone, DoctorNameSnapshot = record.Doctor.FullName, ExamDateSnapshot = record.ExamDate, TotalAmount = total, FinalAmount = total, InvoiceDetails = lines });
        await db.SaveChangesAsync(ct);
    }
}

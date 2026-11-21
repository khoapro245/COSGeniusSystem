using System.Data;
using System.Security.Claims;
using COSGeniusSystem.Commons;
using COSGeniusSystem.DTOs;
using COSGeniusSystem.Exceptions;
using COSGeniusSystem.Models;
using Microsoft.EntityFrameworkCore;
namespace COSGeniusSystem.Services;
public partial class InvoiceService
{
    // Existing schema uses Vietnamese payment labels; expose precisely these supported methods.
    public static readonly string[] PaymentMethods = ["Tiền mặt", "Chuyển khoản"];
    private static int Cashier(ClaimsPrincipal actor)
    {
        if (!actor.IsInRole(RoleConstants.Cashier) && !actor.IsInRole(RoleConstants.Admin)) throw new ForbiddenException();
        return int.TryParse(actor.FindFirstValue(ClaimTypes.NameIdentifier), out var id) && id > 0 ? id : throw new UnauthorizedException();
    }
    private static void Version(Invoice invoice, byte[] expected)
    {
        if (expected == null || expected.Length != 8 || !invoice.RowVersion.SequenceEqual(expected))
            throw new ConflictException("The invoice has changed. Please reload the data.");
        if (invoice.PaymentStatus is not ("UNPAID" or "Chưa thanh toán"))
            throw new ConflictException("The invoice has been paid or cancelled. Please reload.");
    }
    private async Task<Invoice> Locked(int id) => await _context.Invoices.FromSqlInterpolated($"SELECT * FROM dbo.Invoice WITH (UPDLOCK,HOLDLOCK) WHERE InvoiceID={id}")
        .Include(x => x.InvoiceDetails).SingleOrDefaultAsync() ?? throw new NotFoundException("Invoice was not found.");
    public async Task<InvoiceDetailDto> PayAsync(ClaimsPrincipal actor, int id, InvoicePaymentRequest request)
    {
        var cashier = Cashier(actor);
        if (!request.ReceivedInFull || !PaymentMethods.Contains(request.PaymentMethod))
            throw new BadRequestException("Select a payment method and confirm that full payment has been received.");
        await using var tx = await _context.Database.BeginTransactionAsync(IsolationLevel.Serializable);
        var invoice = await Locked(id); Version(invoice, request.RowVersion);
        if (invoice.InvoiceDetails.Count == 0 || invoice.InvoiceDetails.Any(x => !x.IsActive || x.Quantity <= 0 || x.UnitPrice < 0)
            || invoice.InvoiceDetails.Sum(x => x.Quantity * x.UnitPrice) != invoice.TotalAmount
            || invoice.Discount < 0 || invoice.Discount > invoice.TotalAmount || invoice.FinalAmount != invoice.TotalAmount - invoice.Discount)
            throw new ConflictException("The charges or invoice total are invalid. Please review.");
        foreach (var line in invoice.InvoiceDetails)
        {
            if (!((line.ItemType == "HERB_REMEDY" && line.PrescriptionId.HasValue && !line.PlanDetailId.HasValue)
                || (line.ItemType == "SERVICE" && line.PlanDetailId.HasValue && !line.PrescriptionId.HasValue)))
                throw new ConflictException("The charge source is ambiguous. Reconcile the invoice.");
            if (line.PrescriptionId.HasValue && !await _context.HerbalPrescriptions.AnyAsync(p => p.PrescriptionId == line.PrescriptionId && p.Status == "SENT" && p.Record.Status == "COMPLETED"))
                throw new ConflictException("The prescription has not been confirmed in a completed examination.");
            if (line.PrescriptionId.HasValue && (await _context.HerbalPrescriptions.Where(p => p.PrescriptionId == line.PrescriptionId).Select(p => p.RecordId).SingleAsync() != invoice.RecordId
                || await _context.InvoiceDetails.CountAsync(i => i.PrescriptionId == line.PrescriptionId && i.Invoice.PaymentStatus != "CANCELLED" && i.Invoice.PaymentStatus != "Đã hủy") != 1))
                throw new ConflictException("The prescription belongs to another examination or has been invoiced more than once.");
            if (line.PlanDetailId.HasValue)
            {
                var d = await _context.TreatmentPlanDetails.Include(d => d.Plan).SingleAsync(d => d.PlanDetailId == line.PlanDetailId);
                if (d.Plan.TreatmentType is not ("COURSE" or "SINGLE"))
                    throw new ConflictException("Invalid indication type.");
                var eligible = d.Plan.ConfirmedAt.HasValue && (d.Plan.Status is "CONFIRMED" or "IN_PROGRESS" or "COMPLETED") && await _context.MedicalRecords.AnyAsync(r => r.RecordId == invoice.RecordId && r.Status == "COMPLETED") ? d.PlannedQuantity : 0;
                var billed = await _context.InvoiceDetails.Where(i => i.PlanDetailId == d.PlanDetailId && i.Invoice.PaymentStatus != "CANCELLED" && i.Invoice.PaymentStatus != "Đã hủy").SumAsync(i => i.Quantity);
                if (d.Plan.RecordId != invoice.RecordId || billed > eligible) throw new ConflictException("The service is not eligible for payment or has been invoiced more than once.");
            }
        }
        // Null preserves compatibility with clients that pay the whole invoice.
        if (request.InvoiceDetailIds != null)
        {
            var selected = request.InvoiceDetailIds.ToHashSet();
            if (selected.Count == 0 || selected.Count != request.InvoiceDetailIds.Count || selected.Any(id => !invoice.InvoiceDetails.Any(line => line.InvoiceDetailId == id)))
                throw new BadRequestException("Select valid invoice items.");
            if (selected.Count != invoice.InvoiceDetails.Count)
            {
                if (invoice.Discount != 0) throw new ConflictException("A discounted invoice must be paid in full.");
                var remaining = invoice.InvoiceDetails.Where(line => !selected.Contains(line.InvoiceDetailId)).ToList();
                var remainingTotal = remaining.Sum(line => line.Quantity * line.UnitPrice);
                var remainder = new Invoice { RecordId = invoice.RecordId, IssuedDate = DateTime.UtcNow,
                    PaymentStatus = "UNPAID", PaymentMethod = "", TotalAmount = remainingTotal, FinalAmount = remainingTotal,
                    PatientNameSnapshot = invoice.PatientNameSnapshot, PatientCodeSnapshot = invoice.PatientCodeSnapshot,
                    PhoneSnapshot = invoice.PhoneSnapshot, DoctorNameSnapshot = invoice.DoctorNameSnapshot, ExamDateSnapshot = invoice.ExamDateSnapshot };
                _context.Invoices.Add(remainder);
                foreach (var line in remaining) { invoice.InvoiceDetails.Remove(line); remainder.InvoiceDetails.Add(line); }
                invoice.TotalAmount -= remainingTotal; invoice.FinalAmount = invoice.TotalAmount;
            }
        }
        invoice.PaymentStatus = "PAID"; invoice.PaymentMethod = request.PaymentMethod;
        invoice.CashierNameSnapshot = await _context.Users.Where(u => u.UserId == cashier && u.IsActive).Select(u => u.FullName).SingleOrDefaultAsync() ?? throw new ForbiddenException();
        invoice.CashierId = cashier; invoice.PaidAt = DateTime.UtcNow;
        await _context.SaveChangesAsync(); await tx.CommitAsync();
        return await GetInvoiceByIdAsync(id);
    }
    public async Task<InvoiceDetailDto> CancelAsync(ClaimsPrincipal actor, int id, InvoiceCancelRequest request)
    {
        var cashier = Cashier(actor);
        if (string.IsNullOrWhiteSpace(request.Reason) || request.Reason.Length > 500) throw new BadRequestException("Enter a cancellation reason of at most 500 characters.");
        await using var tx = await _context.Database.BeginTransactionAsync(IsolationLevel.Serializable);
        var invoice = await Locked(id); Version(invoice, request.RowVersion);
        invoice.PaymentStatus = "CANCELLED"; invoice.CancelledBy = cashier; invoice.CancelledAt = DateTime.UtcNow; invoice.CancellationReason = request.Reason.Trim();
        foreach (var line in invoice.InvoiceDetails) line.IsActive = false;
        await _context.SaveChangesAsync();
        // Reissue only eligible debts; cancelled snapshots are reused by the common accrual logic.
        var previousIds = await _context.Invoices.Where(i => i.RecordId == invoice.RecordId).Select(i => i.InvoiceId).ToListAsync();
        await InvoiceAccrual.GenerateAsync(_context, invoice.RecordId);
        var replacement = await _context.Invoices.Include(i => i.InvoiceDetails).SingleOrDefaultAsync(i => i.RecordId == invoice.RecordId && !previousIds.Contains(i.InvoiceId));
        if (invoice.Discount > 0)
        {
            if (replacement == null || replacement.TotalAmount != invoice.TotalAmount || replacement.InvoiceDetails.Count != invoice.InvoiceDetails.Count)
                throw new ConflictException("The original invoice has discounts and ineligible charges; reconcile it before cancellation.");
            replacement.Discount = invoice.Discount; replacement.FinalAmount = invoice.FinalAmount;
        }
        if (replacement != null)
        {
            replacement.PatientNameSnapshot = invoice.PatientNameSnapshot ?? replacement.PatientNameSnapshot;
            replacement.PatientCodeSnapshot = invoice.PatientCodeSnapshot ?? replacement.PatientCodeSnapshot;
            replacement.PhoneSnapshot = invoice.PhoneSnapshot ?? replacement.PhoneSnapshot;
            replacement.DoctorNameSnapshot = invoice.DoctorNameSnapshot ?? replacement.DoctorNameSnapshot;
            replacement.ExamDateSnapshot = invoice.ExamDateSnapshot ?? replacement.ExamDateSnapshot;
            await _context.SaveChangesAsync();
        }
        await tx.CommitAsync(); return await GetInvoiceByIdAsync(id);
    }    public async Task<int> BackfillAsync()
    {
        var ids = await _context.MedicalRecords.OrderBy(r => r.RecordId).Select(r => r.RecordId).ToListAsync();
        foreach (var id in ids)
        {
            await using var tx = await _context.Database.BeginTransactionAsync(IsolationLevel.Serializable);
            await InvoiceAccrual.GenerateAsync(_context, id); await tx.CommitAsync();
            _context.ChangeTracker.Clear();
        }
        return ids.Count;
    }
}

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
            throw new ConflictException("Hóa đơn đã thay đổi. Vui lòng tải lại dữ liệu.");
        if (invoice.PaymentStatus is not ("UNPAID" or "Chưa thanh toán"))
            throw new ConflictException("Hóa đơn đã được thanh toán hoặc hủy. Vui lòng tải lại.");
    }
    private async Task<Invoice> Locked(int id) => await _context.Invoices.FromSqlInterpolated($"SELECT * FROM dbo.Invoice WITH (UPDLOCK,HOLDLOCK) WHERE InvoiceID={id}")
        .Include(x => x.InvoiceDetails).SingleOrDefaultAsync() ?? throw new NotFoundException("Không tìm thấy hóa đơn.");
    public async Task<InvoiceDetailDto> PayAsync(ClaimsPrincipal actor, int id, InvoicePaymentRequest request)
    {
        var cashier = Cashier(actor);
        if (!request.ReceivedInFull || !PaymentMethods.Contains(request.PaymentMethod))
            throw new BadRequestException("Chọn phương thức và xác nhận đã nhận đủ tiền.");
        await using var tx = await _context.Database.BeginTransactionAsync(IsolationLevel.Serializable);
        var invoice = await Locked(id); Version(invoice, request.RowVersion);
        if (invoice.InvoiceDetails.Count == 0 || invoice.InvoiceDetails.Any(x => !x.IsActive || x.Quantity <= 0 || x.UnitPrice < 0)
            || invoice.InvoiceDetails.Sum(x => x.Quantity * x.UnitPrice) != invoice.TotalAmount
            || invoice.Discount < 0 || invoice.Discount > invoice.TotalAmount || invoice.FinalAmount != invoice.TotalAmount - invoice.Discount)
            throw new ConflictException("Các khoản thu hoặc tổng hóa đơn không hợp lệ. Cần kiểm tra lại.");
        foreach (var line in invoice.InvoiceDetails)
        {
            if (!((line.ItemType == "HERB_REMEDY" && line.PrescriptionId.HasValue && !line.PlanDetailId.HasValue)
                || (line.ItemType == "SERVICE" && line.PlanDetailId.HasValue && !line.PrescriptionId.HasValue)))
                throw new ConflictException("Khoản thu không xác định đúng nguồn. Cần đối chiếu hóa đơn.");
            if (line.PrescriptionId.HasValue && !await _context.MedicineDispenses.AnyAsync(d => d.PrescriptionId == line.PrescriptionId && d.Status == "COMPLETED"))
                throw new ConflictException("Thuốc chưa bốc xong hợp lệ.");
            if (line.PrescriptionId.HasValue && (await _context.HerbalPrescriptions.Where(p => p.PrescriptionId == line.PrescriptionId).Select(p => p.RecordId).SingleAsync() != invoice.RecordId
                || await _context.InvoiceDetails.CountAsync(i => i.PrescriptionId == line.PrescriptionId && i.Invoice.PaymentStatus != "CANCELLED" && i.Invoice.PaymentStatus != "Đã hủy") != 1))
                throw new ConflictException("Đơn thuốc sai lần khám hoặc bị lập trùng.");
            if (line.PlanDetailId.HasValue)
            {
                var d = await _context.TreatmentPlanDetails.Include(d => d.Plan).SingleAsync(d => d.PlanDetailId == line.PlanDetailId);
                if (d.Plan.TreatmentType is not ("COURSE" or "SINGLE"))
                    throw new ConflictException("Loại chỉ định không hợp lệ.");
                var eligible = d.Plan.TreatmentType == "COURSE"
                    ? (d.Plan.ConfirmedAt.HasValue && d.Plan.Status is "CONFIRMED" or "IN_PROGRESS" or "COMPLETED"
                        && await _context.MedicalRecords.AnyAsync(r => r.RecordId == invoice.RecordId && r.Status == "COMPLETED") ? d.PlannedQuantity : 0)
                    : await _context.UsageHistories.Where(u => u.PlanDetailId == d.PlanDetailId && u.Session.Status == "COMPLETED").SumAsync(u => (int?)u.Quantity) ?? 0;
                var billed = await _context.InvoiceDetails.Where(i => i.PlanDetailId == d.PlanDetailId && i.Invoice.PaymentStatus != "CANCELLED" && i.Invoice.PaymentStatus != "Đã hủy").SumAsync(i => i.Quantity);
                if (d.Plan.RecordId != invoice.RecordId || billed > eligible) throw new ConflictException("Dịch vụ chưa đủ điều kiện thu hoặc bị lập trùng.");
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
        if (string.IsNullOrWhiteSpace(request.Reason) || request.Reason.Length > 500) throw new BadRequestException("Nhập lý do hủy tối đa 500 ký tự.");
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
                throw new ConflictException("Hóa đơn cũ có giảm giá và khoản chưa đủ điều kiện; cần đối chiếu trước khi hủy.");
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

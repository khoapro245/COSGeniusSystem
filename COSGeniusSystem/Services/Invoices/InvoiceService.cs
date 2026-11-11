using COSGeniusSystem.Commons;
using Microsoft.EntityFrameworkCore;
using COSGeniusSystem.Data;
using COSGeniusSystem.DTOs;
using COSGeniusSystem.Exceptions;

namespace COSGeniusSystem.Services;

public partial class InvoiceService : IInvoiceService
{
    private readonly ApplicationDbContext _context;

    public InvoiceService(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<PaginatedResult<InvoiceListDto>> GetInvoicesAsync(InvoiceQueryParams queryParams)
    {
        var page = queryParams.Page <= 0 ? 1 : queryParams.Page;
        var pageSize = Math.Clamp(queryParams.PageSize, 1, 100);
        if (queryParams.FromDate > queryParams.ToDate) throw new BadRequestException("Khoảng ngày không hợp lệ.");

        var query = _context.Invoices
            .AsNoTracking()
            .AsQueryable();

        if (queryParams.FromDate.HasValue)
        {
            query = query.Where(i => i.IssuedDate >= queryParams.FromDate.Value.Date.AddHours(-7));
        }

        if (queryParams.ToDate.HasValue)
        {
            query = query.Where(i => i.IssuedDate < queryParams.ToDate.Value.Date.AddDays(1).AddHours(-7));
        }

        if (!string.IsNullOrWhiteSpace(queryParams.PaymentStatus))
        {
            var status = queryParams.PaymentStatus.Trim();
            query = status switch {
                "PAID" or "Đã thanh toán" => query.Where(i => i.PaymentStatus == "PAID" || i.PaymentStatus == "Đã thanh toán"),
                "UNPAID" or "Chưa thanh toán" => query.Where(i => i.PaymentStatus == "UNPAID" || i.PaymentStatus == "Chưa thanh toán"),
                "CANCELLED" or "Đã hủy" => query.Where(i => i.PaymentStatus == "CANCELLED" || i.PaymentStatus == "Đã hủy"),
                _ => throw new BadRequestException("Trạng thái hóa đơn không hợp lệ.")
            };
        }

        if (!string.IsNullOrWhiteSpace(queryParams.Keyword))
        {
            var keyword = queryParams.Keyword.Trim();
            query = query.Where(i => (i.InvoiceCode != null && i.InvoiceCode.Contains(keyword)) ||
                                     i.Record.Patient.FullName.Contains(keyword) || (i.Record.Patient.Phone != null && i.Record.Patient.Phone.Contains(keyword)) || (i.Record.Patient.PatientCode != null && i.Record.Patient.PatientCode.Contains(keyword)));
        }

        var totalCount = await query.CountAsync();

        var items = await query
            .OrderByDescending(i => i.IssuedDate).ThenByDescending(i => i.InvoiceId)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(i => new InvoiceListDto
            {
                InvoiceId = i.InvoiceId, RecordId = i.RecordId, ExamDate = i.ExamDateSnapshot ?? i.Record.ExamDate, PatientCode = i.PatientCodeSnapshot ?? i.Record.Patient.PatientCode, Phone = i.PhoneSnapshot ?? i.Record.Patient.Phone, PaidAt = i.PaidAt,
                InvoiceCode = i.InvoiceCode ?? string.Empty,
                PatientName = i.PatientNameSnapshot ?? i.Record.Patient.FullName,
                CashierName = i.CashierNameSnapshot ?? (i.Cashier == null ? "" : i.Cashier.FullName),
                TotalAmount = i.TotalAmount,
                Discount = i.Discount,
                FinalAmount = i.FinalAmount,
                PaymentMethod = i.PaymentMethod,
                PaymentStatus = i.PaymentStatus,
                IssuedDate = i.IssuedDate
            })
            .ToListAsync();

        return new PaginatedResult<InvoiceListDto>(items, totalCount, page, pageSize);
    }

    public async Task<InvoiceDetailDto> GetInvoiceByIdAsync(int id)
    {
        var invoice = await _context.Invoices
            .AsNoTracking()
            .Include(i => i.Cashier)
            .Include(i => i.Record)
                .ThenInclude(r => r.Doctor)
            .Include(i => i.Record)
                .ThenInclude(r => r.Patient)
            .Include(i => i.InvoiceDetails)
            .FirstOrDefaultAsync(i => i.InvoiceId == id);

        if (invoice == null)
        {
            throw new NotFoundException($"Không tìm thấy hóa đơn với ID: {id}");
        }

        var dto = new InvoiceDetailDto
        {
            InvoiceId = invoice.InvoiceId, RecordId = invoice.RecordId, ExamDate = invoice.ExamDateSnapshot ?? invoice.Record.ExamDate, Phone = invoice.PhoneSnapshot ?? invoice.Record.Patient.Phone, PaidAt = invoice.PaidAt, RowVersion = invoice.RowVersion, CancellationReason = invoice.CancellationReason,
            InvoiceCode = invoice.InvoiceCode ?? string.Empty,
            IssuedDate = invoice.IssuedDate,
            PaymentMethod = invoice.PaymentMethod,
            PaymentStatus = invoice.PaymentStatus,
            PatientName = invoice.PatientNameSnapshot ?? invoice.Record.Patient.FullName,
            PatientCode = invoice.PatientCodeSnapshot ?? invoice.Record.Patient.PatientCode,
            DoctorName = invoice.DoctorNameSnapshot ?? invoice.Record.Doctor.FullName,
            CashierName = invoice.CashierNameSnapshot ?? invoice.Cashier?.FullName ?? string.Empty,
            TotalAmount = invoice.TotalAmount,
            Discount = invoice.Discount,
            FinalAmount = invoice.FinalAmount,
            Items = invoice.InvoiceDetails.OrderBy(d => d.InvoiceDetailId).Select(d => new InvoiceItemDto
            {
                InvoiceDetailId = d.InvoiceDetailId, TreatmentType = d.TreatmentTypeSnapshot, Unit = d.UnitSnapshot, Ingredients = d.IngredientsJson == null ? [] : System.Text.Json.JsonSerializer.Deserialize<List<InvoiceIngredient>>(d.IngredientsJson) ?? [],
                ItemType = d.ItemType,
                PrescriptionId = d.PrescriptionId,
                PlanDetailId = d.PlanDetailId,
                ItemName = d.ItemName,
                Quantity = d.Quantity,
                UnitPrice = d.UnitPrice,
                SubTotal = d.SubTotal ?? (d.Quantity * d.UnitPrice)
            }).ToList()
        };

        return dto;
    }
}

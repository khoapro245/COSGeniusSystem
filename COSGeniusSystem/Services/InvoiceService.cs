using Microsoft.EntityFrameworkCore;
using COSGeniusSystem.Commons;
using COSGeniusSystem.Data;
using COSGeniusSystem.DTOs.Invoices;
using COSGeniusSystem.Exceptions;
using COSGeniusSystem.Services.Interfaces;

namespace COSGeniusSystem.Services;

public class InvoiceService : IInvoiceService
{
    private readonly ApplicationDbContext _context;

    public InvoiceService(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<PaginatedResult<InvoiceListDto>> GetInvoicesAsync(InvoiceQueryParams queryParams)
    {
        var page = queryParams.Page <= 0 ? 1 : queryParams.Page;
        var pageSize = queryParams.PageSize <= 0 ? 20 : queryParams.PageSize;

        var query = _context.Invoices
            .AsNoTracking()
            .AsQueryable();

        if (queryParams.FromDate.HasValue)
        {
            query = query.Where(i => i.IssuedDate >= queryParams.FromDate.Value);
        }

        if (queryParams.ToDate.HasValue)
        {
            query = query.Where(i => i.IssuedDate <= queryParams.ToDate.Value);
        }

        if (!string.IsNullOrWhiteSpace(queryParams.PaymentStatus))
        {
            query = query.Where(i => i.PaymentStatus == queryParams.PaymentStatus.Trim());
        }

        if (!string.IsNullOrWhiteSpace(queryParams.Keyword))
        {
            var keyword = queryParams.Keyword.Trim();
            query = query.Where(i => i.InvoiceCode.Contains(keyword) ||
                                     i.ExamSheet.Record.Patient.FullName.Contains(keyword));
        }

        var totalCount = await query.CountAsync();

        var items = await query
            .OrderByDescending(i => i.IssuedDate)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(i => new InvoiceListDto
            {
                InvoiceId = i.InvoiceId,
                InvoiceCode = i.InvoiceCode,
                PatientName = i.ExamSheet.Record.Patient.FullName,
                CashierName = i.Cashier.FullName,
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
            .Include(i => i.ExamSheet)
                .ThenInclude(e => e.Doctor)
            .Include(i => i.ExamSheet)
                .ThenInclude(e => e.Record)
                    .ThenInclude(r => r.Patient)
            .Include(i => i.InvoiceDetails)
            .FirstOrDefaultAsync(i => i.InvoiceId == id);

        if (invoice == null)
        {
            throw new NotFoundException($"Không tìm thấy hóa đơn với ID: {id}");
        }

        var dto = new InvoiceDetailDto
        {
            InvoiceId = invoice.InvoiceId,
            InvoiceCode = invoice.InvoiceCode,
            IssuedDate = invoice.IssuedDate,
            PaymentMethod = invoice.PaymentMethod,
            PaymentStatus = invoice.PaymentStatus,
            PatientName = invoice.ExamSheet?.Record?.Patient?.FullName ?? string.Empty,
            PatientCode = invoice.ExamSheet?.Record?.Patient?.PatientCode,
            DoctorName = invoice.ExamSheet?.Doctor?.FullName ?? string.Empty,
            CashierName = invoice.Cashier?.FullName ?? string.Empty,
            TotalAmount = invoice.TotalAmount,
            Discount = invoice.Discount,
            FinalAmount = invoice.FinalAmount,
            Items = invoice.InvoiceDetails.Select(d => new InvoiceItemDto
            {
                InvoiceDetailId = d.InvoiceDetailId,
                ItemType = d.ItemType,
                ReferenceId = d.ReferenceId,
                ItemName = d.ItemName,
                Quantity = d.Quantity,
                UnitPrice = d.UnitPrice,
                SubTotal = d.SubTotal ?? (d.Quantity * d.UnitPrice)
            }).ToList()
        };

        return dto;
    }
}

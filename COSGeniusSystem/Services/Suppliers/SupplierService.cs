using COSGeniusSystem.Data;
using COSGeniusSystem.DTOs.Suppliers;
using COSGeniusSystem.Models;
using Microsoft.EntityFrameworkCore;

namespace COSGeniusSystem.Services.Suppliers;

public class SupplierService : ISupplierService
{
    private readonly ApplicationDbContext _context;

    public SupplierService(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<List<SupplierResponseDto>> GetSuppliersAsync(SupplierFilterQuery query, CancellationToken ct = default)
    {
        var q = _context.Suppliers.AsNoTracking();

        if (query.IsActive.HasValue)
            q = q.Where(s => s.IsActive == query.IsActive.Value);

        if (!string.IsNullOrWhiteSpace(query.Keyword))
        {
            var kw = query.Keyword.Trim().ToLower();
            q = q.Where(s =>
                s.SupplierCode.ToLower().Contains(kw) ||
                s.SupplierName.ToLower().Contains(kw) ||
                (s.Phone != null && s.Phone.ToLower().Contains(kw)) ||
                (s.Email != null && s.Email.ToLower().Contains(kw)) ||
                (s.TaxCode != null && s.TaxCode.ToLower().Contains(kw)));
        }

        var page = query.Page < 1 ? 1 : query.Page;
        var pageSize = query.PageSize < 1 ? 20 : Math.Min(query.PageSize, 100);

        return await q
            .OrderBy(s => s.SupplierCode)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(s => new SupplierResponseDto
            {
                SupplierId = s.SupplierId,
                SupplierCode = s.SupplierCode,
                SupplierName = s.SupplierName,
                Phone = s.Phone,
                Email = s.Email,
                Address = s.Address,
                TaxCode = s.TaxCode,
                IsActive = s.IsActive
            })
            .ToListAsync(ct);
    }

    public async Task<SupplierDetailDto?> GetSupplierByIdAsync(int id, CancellationToken ct = default)
    {
        var s = await _context.Suppliers
            .AsNoTracking()
            .Include(x => x.PurchaseOrders)
            .FirstOrDefaultAsync(x => x.SupplierId == id, ct);

        if (s is null) return null;

        return MapToDetailDto(s);
    }

    public async Task<SupplierDetailDto?> GetSupplierByCodeAsync(string code, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(code)) return null;

        var cleanCode = code.Trim().ToLower();
        var s = await _context.Suppliers
            .AsNoTracking()
            .Include(x => x.PurchaseOrders)
            .FirstOrDefaultAsync(x => x.SupplierCode.ToLower() == cleanCode, ct);

        if (s is null) return null;

        return MapToDetailDto(s);
    }

    public async Task<(SupplierResponseDto? Dto, string? Error)> CreateSupplierAsync(CreateSupplierRequest request, CancellationToken ct = default)
    {
        string supplierCode;

        if (string.IsNullOrWhiteSpace(request.SupplierCode))
        {
            // Tự sinh mã nhà cung cấp theo format NCC00001, NCC00002...
            var count = await _context.Suppliers.CountAsync(ct);
            var candidateNum = count + 1;
            supplierCode = $"NCC{candidateNum:D5}";

            while (await _context.Suppliers.AnyAsync(s => s.SupplierCode == supplierCode, ct))
            {
                candidateNum++;
                supplierCode = $"NCC{candidateNum:D5}";
            }
        }
        else
        {
            supplierCode = request.SupplierCode.Trim().ToUpperInvariant();
            var exists = await _context.Suppliers
                .AnyAsync(s => s.SupplierCode.ToLower() == supplierCode.ToLower(), ct);

            if (exists)
                return (null, "Mã nhà cung cấp đã tồn tại trong hệ thống.");
        }

        if (string.IsNullOrWhiteSpace(request.SupplierName))
            return (null, "Tên nhà cung cấp không được để trống.");

        var supplier = new Supplier
        {
            SupplierCode = supplierCode,
            SupplierName = request.SupplierName.Trim(),
            Phone = string.IsNullOrWhiteSpace(request.Phone) ? null : request.Phone.Trim(),
            Email = string.IsNullOrWhiteSpace(request.Email) ? null : request.Email.Trim(),
            Address = string.IsNullOrWhiteSpace(request.Address) ? null : request.Address.Trim(),
            TaxCode = string.IsNullOrWhiteSpace(request.TaxCode) ? null : request.TaxCode.Trim(),
            IsActive = request.IsActive
        };

        _context.Suppliers.Add(supplier);
        await _context.SaveChangesAsync(ct);

        var dto = new SupplierResponseDto
        {
            SupplierId = supplier.SupplierId,
            SupplierCode = supplier.SupplierCode,
            SupplierName = supplier.SupplierName,
            Phone = supplier.Phone,
            Email = supplier.Email,
            Address = supplier.Address,
            TaxCode = supplier.TaxCode,
            IsActive = supplier.IsActive
        };

        return (dto, null);
    }

    public async Task<(SupplierResponseDto? Dto, string? Error)> UpdateSupplierAsync(int id, UpdateSupplierRequest request, CancellationToken ct = default)
    {
        var supplier = await _context.Suppliers.FirstOrDefaultAsync(s => s.SupplierId == id, ct);
        if (supplier is null)
            return (null, null);

        if (string.IsNullOrWhiteSpace(request.SupplierName))
            return (null, "Tên nhà cung cấp không được để trống.");

        supplier.SupplierName = request.SupplierName.Trim();
        supplier.Phone = string.IsNullOrWhiteSpace(request.Phone) ? null : request.Phone.Trim();
        supplier.Email = string.IsNullOrWhiteSpace(request.Email) ? null : request.Email.Trim();
        supplier.Address = string.IsNullOrWhiteSpace(request.Address) ? null : request.Address.Trim();
        supplier.TaxCode = string.IsNullOrWhiteSpace(request.TaxCode) ? null : request.TaxCode.Trim();
        supplier.IsActive = request.IsActive;

        await _context.SaveChangesAsync(ct);

        var dto = new SupplierResponseDto
        {
            SupplierId = supplier.SupplierId,
            SupplierCode = supplier.SupplierCode,
            SupplierName = supplier.SupplierName,
            Phone = supplier.Phone,
            Email = supplier.Email,
            Address = supplier.Address,
            TaxCode = supplier.TaxCode,
            IsActive = supplier.IsActive
        };

        return (dto, null);
    }

    public async Task<(bool Success, bool IsDeactivated, string? Error)> DeleteSupplierAsync(int id, CancellationToken ct = default)
    {
        var supplier = await _context.Suppliers
            .Include(s => s.PurchaseOrders)
            .FirstOrDefaultAsync(s => s.SupplierId == id, ct);

        if (supplier is null)
            return (false, false, "Không tìm thấy nhà cung cấp.");

        var hasPurchaseOrders = supplier.PurchaseOrders.Any();

        if (hasPurchaseOrders)
        {
            // Có dữ liệu ràng buộc lịch sử PO: chuyển trạng thái ngưng hoạt động
            supplier.IsActive = false;
            await _context.SaveChangesAsync(ct);
            return (true, true, null);
        }

        // Chưa có dữ liệu ràng buộc: xóa hẳn khỏi CSDL
        _context.Suppliers.Remove(supplier);
        await _context.SaveChangesAsync(ct);
        return (true, false, null);
    }

    private static SupplierDetailDto MapToDetailDto(Supplier s)
    {
        return new SupplierDetailDto
        {
            SupplierId = s.SupplierId,
            SupplierCode = s.SupplierCode,
            SupplierName = s.SupplierName,
            Phone = s.Phone,
            Email = s.Email,
            Address = s.Address,
            TaxCode = s.TaxCode,
            IsActive = s.IsActive,
            PurchaseOrders = s.PurchaseOrders
                .OrderByDescending(p => p.OrderDate)
                .Select(p => new SupplierPurchaseOrderDto
                {
                    PoId = p.Poid,
                    PoCode = p.Pocode,
                    OrderDate = p.OrderDate,
                    DeliveryDate = p.DeliveryDate,
                    TotalAmount = p.TotalAmount,
                    Status = p.Status
                })
                .ToList()
        };
    }
}


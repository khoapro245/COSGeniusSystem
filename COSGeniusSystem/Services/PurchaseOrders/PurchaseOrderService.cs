using COSGeniusSystem.Data;
using COSGeniusSystem.DTOs;
using COSGeniusSystem.Exceptions;
using COSGeniusSystem.Models;
using Microsoft.EntityFrameworkCore;

namespace COSGeniusSystem.Services;

public class PurchaseOrderService : IPurchaseOrderService
{
    private readonly ApplicationDbContext _context;

    public PurchaseOrderService(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<List<PurchaseOrderDto>> GetPurchaseOrdersAsync(string? status = null, string? search = null, CancellationToken ct = default)
    {
        var query = _context.PurchaseOrders
            .AsNoTracking()
            .Include(p => p.Supplier)
            .Include(p => p.CreatedByNavigation)
            .Include(p => p.Podetails)
                .ThenInclude(d => d.Herb)
            .Include(p => p.GoodsReceipts)
                .ThenInclude(g => g.ReceiptDetails)
                    .ThenInclude(rd => rd.Batch)
            .OrderByDescending(p => p.OrderDate)
            .ThenByDescending(p => p.Poid)
            .AsQueryable();

        var list = await query.ToListAsync(ct);
        var dtos = list.Select(MapToDto).ToList();

        // Filter by keyword
        if (!string.IsNullOrWhiteSpace(search))
        {
            var q = search.Trim().ToLower();
            dtos = dtos.Where(p =>
                p.Pocode.ToLower().Contains(q) ||
                p.SupplierName.ToLower().Contains(q) ||
                p.Details.Any(d => d.HerbName.ToLower().Contains(q) || d.HerbCode.ToLower().Contains(q))
            ).ToList();
        }

        // Filter by status
        if (!string.IsNullOrWhiteSpace(status) && !status.Equals("all", StringComparison.OrdinalIgnoreCase))
        {
            var s = status.Trim().ToLower();
            // [hieppm] Start: Hỗ trợ đầy đủ tham số lọc trạng thái giữa Frontend và Backend
            if (s == "pending" || s == "pending_delivery" || s == "chờ nhận")
                dtos = dtos.Where(p => p.ProgressStatus == "Chờ nhận").ToList();
            else if (s == "partial" || s == "nhận 1 phần" || s == "nhận một phần")
                dtos = dtos.Where(p => p.ProgressStatus == "Nhận một phần" || p.ProgressStatus == "Nhận 1 phần").ToList();
            else if (s == "completed" || s == "đã hoàn thành" || s == "hoàn thành")
                dtos = dtos.Where(p => p.ProgressStatus == "Đã hoàn thành").ToList();
            else if (s == "draft" || s == "nháp")
                dtos = dtos.Where(p => p.Status.Equals("Nháp", StringComparison.OrdinalIgnoreCase) || p.Status.Equals("DRAFT", StringComparison.OrdinalIgnoreCase)).ToList();
            // [hieppm] End: Hỗ trợ đầy đủ tham số lọc trạng thái giữa Frontend và Backend
        }

        return dtos;
    }

    public async Task<PurchaseOrderDto?> GetPurchaseOrderDetailAsync(string idOrCode, CancellationToken ct = default)
    {
        var query = _context.PurchaseOrders
            .AsNoTracking()
            .Include(p => p.Supplier)
            .Include(p => p.CreatedByNavigation)
            .Include(p => p.Podetails)
                .ThenInclude(d => d.Herb)
            .Include(p => p.GoodsReceipts)
                .ThenInclude(g => g.ReceiptDetails)
                    .ThenInclude(rd => rd.Batch)
            .AsQueryable();

        PurchaseOrder? po;
        if (int.TryParse(idOrCode, out int poid))
        {
            po = await query.FirstOrDefaultAsync(p => p.Poid == poid, ct);
        }
        else
        {
            po = await query.FirstOrDefaultAsync(p => p.Pocode.ToLower() == idOrCode.ToLower(), ct);
        }

        return po == null ? null : MapToDto(po);
    }

    public async Task<PurchaseOrderDto> CreatePOAsync(int userId, CreatePORequest request, CancellationToken ct = default)
    {
        // [hieppm] Start: Validate chặt chẽ vị thuốc, trùng dòng, trạng thái và sinh mã POCode không trùng
        await ValidatePOItemsAsync(request.Items, ct);

        var supplier = await _context.Suppliers.FindAsync(new object[] { request.SupplierId }, ct);
        if (supplier == null)
        {
            throw new NotFoundException($"Nhà cung cấp ID {request.SupplierId} không tồn tại.");
        }

        var normalizedStatus = NormalizeAllowedPOStatus(request.Status, defaultStatus: "Nháp");

        // Generate next unique POCode e.g. PO00016
        var nextSeq = (await _context.PurchaseOrders.MaxAsync(p => (int?)p.Poid, ct) ?? 0) + 1;
        var poCode = $"PO{nextSeq:D5}";
        while (await _context.PurchaseOrders.AnyAsync(p => p.Pocode == poCode, ct))
        {
            nextSeq++;
            poCode = $"PO{nextSeq:D5}";
        }

        decimal totalAmount = request.Items.Sum(i => i.OrderQuantity * i.UnitPrice);

        var po = new PurchaseOrder
        {
            Pocode = poCode,
            SupplierId = request.SupplierId,
            CreatedBy = userId > 0 ? userId : 1, // Default fallback user
            OrderDate = DateTime.Now,
            DeliveryDate = request.DeliveryDate,
            TotalAmount = totalAmount,
            Status = normalizedStatus,
        };

        foreach (var item in request.Items)
        {
            po.Podetails.Add(new Podetail
            {
                HerbId = item.HerbId,
                OrderQuantity = item.OrderQuantity,
                UnitPrice = item.UnitPrice,
            });
        }

        _context.PurchaseOrders.Add(po);
        await _context.SaveChangesAsync(ct);

        return (await GetPurchaseOrderDetailAsync(po.Poid.ToString(), ct))!;
        // [hieppm] End: Validate chặt chẽ vị thuốc, trùng dòng, trạng thái và sinh mã POCode không trùng
    }

    public async Task<PurchaseOrderDto> UpdatePOAsync(int id, UpdatePORequest request, CancellationToken ct = default)
    {
        var po = await _context.PurchaseOrders
            .Include(p => p.Podetails)
            .FirstOrDefaultAsync(p => p.Poid == id, ct);

        if (po == null)
        {
            throw new NotFoundException($"Không tìm thấy đơn mua hàng ID {id}.");
        }

        // Strict Constraint: Chỉ được sửa khi PO đang ở trạng thái Nháp
        if (!IsDraftStatus(po.Status))
        {
            throw new BadRequestException($"Đơn mua hàng '{po.Pocode}' đang ở trạng thái '{po.Status}'. Chỉ được phép chỉnh sửa đơn mua hàng ở trạng thái 'Nháp'.");
        }

        // [hieppm] Start: Validate danh sách vị thuốc & trạng thái khi cập nhật PO nháp
        await ValidatePOItemsAsync(request.Items, ct);

        var supplier = await _context.Suppliers.FindAsync(new object[] { request.SupplierId }, ct);
        if (supplier == null)
        {
            throw new NotFoundException($"Nhà cung cấp ID {request.SupplierId} không tồn tại.");
        }

        po.SupplierId = request.SupplierId;
        po.DeliveryDate = request.DeliveryDate;
        if (!string.IsNullOrWhiteSpace(request.Status))
        {
            po.Status = NormalizeAllowedPOStatus(request.Status, defaultStatus: po.Status);
        }

        // Remove old details and insert updated
        _context.Podetails.RemoveRange(po.Podetails);
        po.Podetails.Clear();

        decimal total = 0;
        foreach (var item in request.Items)
        {
            total += item.OrderQuantity * item.UnitPrice;
            po.Podetails.Add(new Podetail
            {
                Poid = po.Poid,
                HerbId = item.HerbId,
                OrderQuantity = item.OrderQuantity,
                UnitPrice = item.UnitPrice,
            });
        }
        po.TotalAmount = total;

        await _context.SaveChangesAsync(ct);
        return (await GetPurchaseOrderDetailAsync(po.Poid.ToString(), ct))!;
        // [hieppm] End: Validate danh sách vị thuốc & trạng thái khi cập nhật PO nháp
    }

    public async Task DeletePOAsync(int id, CancellationToken ct = default)
    {
        var po = await _context.PurchaseOrders
            .Include(p => p.Podetails)
            .FirstOrDefaultAsync(p => p.Poid == id, ct);

        if (po == null)
        {
            throw new NotFoundException($"Không tìm thấy đơn mua hàng ID {id}.");
        }

        // Strict Constraint: Chỉ được xóa khi PO đang ở trạng thái Nháp
        if (!IsDraftStatus(po.Status))
        {
            throw new BadRequestException($"Đơn mua hàng '{po.Pocode}' đang ở trạng thái '{po.Status}'. Chỉ được phép xóa đơn mua hàng ở trạng thái 'Nháp'.");
        }

        _context.Podetails.RemoveRange(po.Podetails);
        _context.PurchaseOrders.Remove(po);
        await _context.SaveChangesAsync(ct);
    }

    // [hieppm] Start: Helper kiểm tra danh sách vị thuốc và trạng thái PO hợp lệ
    private async Task ValidatePOItemsAsync(IReadOnlyCollection<CreatePOItemRequest>? items, CancellationToken ct)
    {
        if (items == null || items.Count == 0)
        {
            throw new BadRequestException("Đơn mua hàng phải có ít nhất một mặt hàng vị thuốc.");
        }

        var seenHerbIds = new HashSet<int>();
        foreach (var item in items)
        {
            if (item.HerbId <= 0)
            {
                throw new BadRequestException("Vị thuốc được chọn không hợp lệ.");
            }
            if (item.OrderQuantity <= 0)
            {
                throw new BadRequestException("Số lượng đặt phải lớn hơn 0.");
            }
            if (item.UnitPrice < 0)
            {
                throw new BadRequestException("Đơn giá không được âm.");
            }
            if (!seenHerbIds.Add(item.HerbId))
            {
                throw new BadRequestException($"Vị thuốc ID {item.HerbId} bị trùng lặp trong đơn mua hàng.");
            }
        }

        var requestedHerbIds = seenHerbIds.ToList();
        var existingHerbIds = await _context.Herbs
            .Where(h => requestedHerbIds.Contains(h.HerbId))
            .Select(h => h.HerbId)
            .ToListAsync(ct);

        var missingHerbId = requestedHerbIds.FirstOrDefault(id => !existingHerbIds.Contains(id));
        if (missingHerbId > 0)
        {
            throw new NotFoundException($"Vị thuốc ID {missingHerbId} không tồn tại.");
        }
    }

    private static string NormalizeAllowedPOStatus(string? status, string defaultStatus)
    {
        if (string.IsNullOrWhiteSpace(status)) return defaultStatus;
        var s = status.Trim();
        if (s.Equals("Nháp", StringComparison.OrdinalIgnoreCase) ||
            s.Equals("DRAFT", StringComparison.OrdinalIgnoreCase) ||
            s.Equals("Mới tạo", StringComparison.OrdinalIgnoreCase))
        {
            return "Nháp";
        }
        if (s.Equals("Chờ nhận", StringComparison.OrdinalIgnoreCase) ||
            s.Equals("Chờ nhận hàng", StringComparison.OrdinalIgnoreCase) ||
            s.Equals("PENDING", StringComparison.OrdinalIgnoreCase))
        {
            return "Chờ nhận";
        }

        throw new BadRequestException($"Trạng thái '{status}' không hợp lệ khi lập hoặc cập nhật đơn mua hàng.");
    }
    // [hieppm] End: Helper kiểm tra danh sách vị thuốc và trạng thái PO hợp lệ

    private static bool IsDraftStatus(string status)
    {
        if (string.IsNullOrWhiteSpace(status)) return true;
        var s = status.Trim().ToLower();
        return s == "nháp" || s == "draft" || s == "mới tạo";
    }

    private static PurchaseOrderDto MapToDto(PurchaseOrder po)
    {
        // Calculate approved receipts
        var approvedReceiptDetails = po.GoodsReceipts
            .Where(g => g.Status.Equals("APPROVED", StringComparison.OrdinalIgnoreCase) ||
                        g.Status.Equals("Đã duyệt", StringComparison.OrdinalIgnoreCase) ||
                        g.Status.Equals("Đã duyệt nhập", StringComparison.OrdinalIgnoreCase))
            .SelectMany(g => g.ReceiptDetails)
            .ToList();

        var detailDtos = new List<PODetailDto>();
        decimal totalOrdered = 0;
        decimal totalReceived = 0;

        // [hieppm] Start: Đồng bộ dữ liệu chi tiết và trạng thái PO theo database
        var dbStatus = (po.Status ?? string.Empty).Trim();
        bool isDbCompleted = dbStatus.Equals("Đã nhận hàng", StringComparison.OrdinalIgnoreCase) ||
                             dbStatus.Equals("Đã hoàn thành", StringComparison.OrdinalIgnoreCase) ||
                             dbStatus.Equals("Hoàn thành", StringComparison.OrdinalIgnoreCase);

        foreach (var d in po.Podetails)
        {
            totalOrdered += d.OrderQuantity;

            // Quantity received for this herb across approved receipts
            decimal receivedForHerb = approvedReceiptDetails
                .Where(rd => rd.Batch != null && rd.Batch.HerbId == d.HerbId)
                .Sum(rd => rd.Quantity);

            // In case Batch wasn't fully loaded, fallback by batch id if batch matches
            if (receivedForHerb == 0 && approvedReceiptDetails.Any())
            {
                receivedForHerb = approvedReceiptDetails
                    .Where(rd => rd.Batch?.HerbId == d.HerbId)
                    .Sum(rd => rd.Quantity);
            }

            // Nếu đơn hàng trong DB đã đánh dấu là "Đã nhận hàng" / "Đã hoàn thành" nhưng chưa có phiếu duyệt lẻ, đồng bộ số lượng đã nhận
            if (isDbCompleted && receivedForHerb == 0)
            {
                receivedForHerb = d.OrderQuantity;
            }

            totalReceived += receivedForHerb;
            decimal remaining = Math.Max(0, d.OrderQuantity - receivedForHerb);

            detailDtos.Add(new PODetailDto
            {
                PodetailId = d.PodetailId,
                Poid = d.Poid,
                HerbId = d.HerbId,
                HerbCode = d.Herb?.HerbCode ?? $"VT{d.HerbId:D5}",
                HerbName = d.Herb?.CommonName ?? "Vị thuốc",
                Unit = "kg",
                OrderQuantity = d.OrderQuantity,
                ReceivedQuantity = receivedForHerb,
                RemainingQuantity = remaining,
                UnitPrice = d.UnitPrice,
                LineTotal = d.OrderQuantity * d.UnitPrice
            });
        }

        string progressStatus;
        string detailStatus;

        if (isDbCompleted || (detailDtos.Count > 0 && detailDtos.All(d => d.RemainingQuantity <= 0)))
        {
            progressStatus = "Đã hoàn thành";
            detailStatus = "Đã hoàn thành nhận hàng";
        }
        else if (IsDraftStatus(dbStatus))
        {
            progressStatus = "Nháp";
            detailStatus = "Bản nháp - Chờ phát hành";
        }
        else if (detailDtos.Any(d => d.ReceivedQuantity > 0) ||
                 dbStatus.Equals("Nhận 1 phần", StringComparison.OrdinalIgnoreCase) ||
                 dbStatus.Equals("Nhận một phần", StringComparison.OrdinalIgnoreCase))
        {
            progressStatus = "Nhận một phần";
            detailStatus = "Đang nhận một phần";
        }
        else
        {
            progressStatus = "Chờ nhận";
            detailStatus = "Chờ nhận hàng";
        }
        // [hieppm] End: Đồng bộ dữ liệu chi tiết và trạng thái PO theo database

        return new PurchaseOrderDto
        {
            Poid = po.Poid,
            Pocode = po.Pocode,
            SupplierId = po.SupplierId,
            SupplierName = po.Supplier?.SupplierName ?? "Nhà cung cấp",
            CreatedBy = po.CreatedBy,
            CreatorName = po.CreatedByNavigation?.FullName ?? "Quản lý kho",
            RequestId = po.RequestId,
            OrderDate = po.OrderDate,
            DeliveryDate = po.DeliveryDate,
            TotalAmount = po.TotalAmount,
            Status = po.Status,
            ProgressStatus = progressStatus,
            DetailStatus = detailStatus,
            Details = detailDtos
        };
    }
}

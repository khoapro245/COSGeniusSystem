// [hieppm] Start: Service xử lý nghiệp vụ cho Đề xuất & Yêu cầu bổ sung kho (ImportRequest)
using COSGeniusSystem.Data;
using COSGeniusSystem.DTOs;
using COSGeniusSystem.Exceptions;
using COSGeniusSystem.Models;
using Microsoft.EntityFrameworkCore;

namespace COSGeniusSystem.Services;

public class ImportRequestService : IImportRequestService
{
    private readonly ApplicationDbContext _context;

    public ImportRequestService(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<List<ImportRequestDto>> GetImportRequestsAsync(string? status = null, CancellationToken ct = default)
    {
        var query = _context.ImportRequests
            .AsNoTracking()
            .Include(r => r.RequestedByNavigation)
            .Include(r => r.ImportRequestDetails)
                .ThenInclude(d => d.Herb)
            .Include(r => r.PurchaseOrders)
            .OrderByDescending(r => r.RequestDate)
            .ThenByDescending(r => r.RequestId)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(status) && !status.Equals("all", StringComparison.OrdinalIgnoreCase))
        {
            var s = status.Trim().ToLower();
            if (s == "pending" || s == "chờ duyệt" || s == "chờ quản lý duyệt")
            {
                query = query.Where(r => r.Status == "Chờ duyệt" || r.Status == "PENDING" || r.Status == "Chờ quản lý duyệt");
            }
            else if (s == "approved" || s == "đã duyệt")
            {
                query = query.Where(r => r.Status == "Đã duyệt" || r.Status == "APPROVED");
            }
            else if (s == "rejected" || s == "từ chối")
            {
                query = query.Where(r => r.Status == "Từ chối" || r.Status == "REJECTED");
            }
        }

        var list = await query.ToListAsync(ct);

        // Nạp trước tồn kho theo HerbId từ bảng Batch để tối ưu hiệu năng (1 query duy nhất)
        var herbIds = list
            .SelectMany(r => r.ImportRequestDetails.Select(d => d.HerbId))
            .Distinct()
            .ToList();

        var stockMap = await _context.Batches
            .AsNoTracking()
            .Where(b => herbIds.Contains(b.HerbId) && (b.Status == "Đạt chuẩn" || b.CurrentQuantity > 0))
            .GroupBy(b => b.HerbId)
            .Select(g => new { HerbId = g.Key, TotalStock = g.Sum(b => b.CurrentQuantity) })
            .ToDictionaryAsync(x => x.HerbId, x => x.TotalStock, ct);

        return list.Select(r => MapToDto(r, stockMap)).ToList();
    }

    public async Task<ImportRequestDto?> GetImportRequestDetailAsync(string idOrCode, CancellationToken ct = default)
    {
        var query = _context.ImportRequests
            .AsNoTracking()
            .Include(r => r.RequestedByNavigation)
            .Include(r => r.ImportRequestDetails)
                .ThenInclude(d => d.Herb)
            .Include(r => r.PurchaseOrders)
            .AsQueryable();

        ImportRequest? request;
        if (int.TryParse(idOrCode, out int requestId))
        {
            request = await query.FirstOrDefaultAsync(r => r.RequestId == requestId, ct);
        }
        else
        {
            request = await query.FirstOrDefaultAsync(r => r.RequestCode.ToLower() == idOrCode.ToLower(), ct);
        }

        if (request == null) return null;

        var herbIds = request.ImportRequestDetails.Select(d => d.HerbId).Distinct().ToList();
        var stockMap = await _context.Batches
            .AsNoTracking()
            .Where(b => herbIds.Contains(b.HerbId) && (b.Status == "Đạt chuẩn" || b.CurrentQuantity > 0))
            .GroupBy(b => b.HerbId)
            .Select(g => new { HerbId = g.Key, TotalStock = g.Sum(b => b.CurrentQuantity) })
            .ToDictionaryAsync(x => x.HerbId, x => x.TotalStock, ct);

        return MapToDto(request, stockMap);
    }

    public async Task<ImportRequestDto> CreateImportRequestAsync(int userId, CreateImportRequestRequest request, CancellationToken ct = default)
    {
        if (request.Items == null || request.Items.Count == 0)
        {
            throw new BadRequestException("Yêu cầu bổ sung kho phải có ít nhất 1 vị thuốc cần đề xuất.");
        }

        foreach (var item in request.Items)
        {
            if (item.HerbId <= 0)
            {
                throw new BadRequestException("Mã vị thuốc không hợp lệ.");
            }
            if (item.RequestedQuantity <= 0)
            {
                throw new BadRequestException("Số lượng đề xuất phải lớn hơn 0.");
            }
        }

        // Sinh mã phiếu yêu cầu YCxxxxx an toàn, chống trùng lặp
        var existingCodes = await _context.ImportRequests
            .Where(r => r.RequestCode.StartsWith("YC"))
            .Select(r => r.RequestCode)
            .ToListAsync(ct);

        int nextSeq = 1;
        foreach (var code in existingCodes)
        {
            if (code.Length > 2 && int.TryParse(code.Substring(2), out int seq))
            {
                if (seq >= nextSeq) nextSeq = seq + 1;
            }
        }

        var requestCode = $"YC{nextSeq:D5}";
        while (await _context.ImportRequests.AnyAsync(r => r.RequestCode == requestCode, ct))
        {
            nextSeq++;
            requestCode = $"YC{nextSeq:D5}";
        }

        var newReq = new ImportRequest
        {
            RequestCode = requestCode,
            RequestedBy = userId > 0 ? userId : 1,
            RequestDate = DateTime.Now,
            Status = "Chờ duyệt",
            Reason = request.Reason ?? "Đề xuất bổ sung dược liệu kho"
        };

        foreach (var item in request.Items)
        {
            newReq.ImportRequestDetails.Add(new ImportRequestDetail
            {
                HerbId = item.HerbId,
                RequestedQuantity = item.RequestedQuantity,
                Note = item.Note
            });
        }

        _context.ImportRequests.Add(newReq);

        // Thông báo cho Quản lý khi có yêu cầu bổ sung kho mới
        var managerUser = await _context.Users
            .FirstOrDefaultAsync(u => u.UserRoles.Any(ur => ur.Role.RoleCode == "MANAGER"), ct);
        if (managerUser != null)
        {
            _context.Notifications.Add(new Notification
            {
                UserId = managerUser.UserId,
                Title = "Yêu cầu bổ sung kho mới chờ duyệt",
                Message = $"Thủ kho đã gửi yêu cầu bổ sung {requestCode}. Vui lòng kiểm tra và duyệt để lập đơn mua hàng.",
                IsRead = false,
                CreatedAt = DateTime.Now
            });
        }

        await _context.SaveChangesAsync(ct);

        return (await GetImportRequestDetailAsync(newReq.RequestId.ToString(), ct))!;
    }

    public async Task<ImportRequestDto> ReviewImportRequestAsync(int managerId, int requestId, ReviewImportRequestRequest request, CancellationToken ct = default)
    {
        var importReq = await _context.ImportRequests
            .Include(r => r.ImportRequestDetails)
            .FirstOrDefaultAsync(r => r.RequestId == requestId, ct);

        if (importReq == null)
        {
            throw new NotFoundException($"Không tìm thấy yêu cầu bổ sung ID {requestId}.");
        }

        if (importReq.Status == "Đã duyệt" && request.IsApproved)
        {
            throw new BadRequestException($"Yêu cầu '{importReq.RequestCode}' đã được phê duyệt trước đó.");
        }

        if (request.IsApproved)
        {
            importReq.Status = "Đã duyệt";
            if (!string.IsNullOrWhiteSpace(request.ManagerNote))
            {
                importReq.Reason = string.IsNullOrWhiteSpace(importReq.Reason)
                    ? $"[Duyệt]: {request.ManagerNote}"
                    : $"{importReq.Reason} | [Duyệt]: {request.ManagerNote}";
            }
        }
        else
        {
            importReq.Status = "Từ chối";
            if (!string.IsNullOrWhiteSpace(request.ManagerNote))
            {
                importReq.Reason = string.IsNullOrWhiteSpace(importReq.Reason)
                    ? $"[Từ chối]: {request.ManagerNote}"
                    : $"{importReq.Reason} | [Từ chối]: {request.ManagerNote}";
            }
        }

        // Thông báo cho người lập (Thủ kho) về kết quả xét duyệt
        if (importReq.RequestedBy > 0)
        {
            var staffUser = await _context.Users.FindAsync(new object[] { importReq.RequestedBy }, ct);
            if (staffUser != null)
            {
                _context.Notifications.Add(new Notification
                {
                    UserId = staffUser.UserId,
                    Title = request.IsApproved ? $"Yêu cầu {importReq.RequestCode} đã được phê duyệt" : $"Yêu cầu {importReq.RequestCode} bị từ chối",
                    Message = request.IsApproved
                        ? $"Yêu cầu bổ sung {importReq.RequestCode} đã được Quản lý duyệt. Quản lý có thể tạo đơn PO từ yêu cầu này."
                        : $"Yêu cầu bổ sung {importReq.RequestCode} đã bị Quản lý từ chối. Lý do: {request.ManagerNote ?? "Chưa phù hợp"}.",
                    IsRead = false,
                    CreatedAt = DateTime.Now
                });
            }
        }

        await _context.SaveChangesAsync(ct);

        return (await GetImportRequestDetailAsync(requestId.ToString(), ct))!;
    }

    private static ImportRequestDto MapToDto(ImportRequest r, Dictionary<int, decimal> stockMap)
    {
        var linkedPo = r.PurchaseOrders.OrderByDescending(p => p.Poid).FirstOrDefault();

        return new ImportRequestDto
        {
            RequestId = r.RequestId,
            RequestCode = r.RequestCode,
            RequestedBy = r.RequestedBy,
            CreatorName = r.RequestedByNavigation?.FullName ?? "Thủ kho",
            RequestDate = r.RequestDate,
            Status = r.Status,
            Reason = r.Reason,
            LinkedPoId = linkedPo?.Poid,
            LinkedPoCode = linkedPo?.Pocode,
            Details = r.ImportRequestDetails.Select(d => new ImportRequestDetailDto
            {
                RequestDetailId = d.RequestDetailId,
                RequestId = d.RequestId,
                HerbId = d.HerbId,
                HerbCode = d.Herb?.HerbCode ?? $"VT{d.HerbId:D5}",
                HerbName = d.Herb?.CommonName ?? "Vị thuốc",
                Unit = d.Herb?.Unit ?? "g",
                RequestedQuantity = d.RequestedQuantity,
                CurrentStockKg = stockMap.TryGetValue(d.HerbId, out var s) ? s : 0,
                Note = d.Note
            }).ToList()
        };
    }
}
// [hieppm] End: Service xử lý nghiệp vụ cho Đề xuất & Yêu cầu bổ sung kho

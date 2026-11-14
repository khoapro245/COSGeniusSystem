using COSGeniusSystem.Data;
using COSGeniusSystem.DTOs;
using COSGeniusSystem.Exceptions;
using COSGeniusSystem.Models;
using Microsoft.EntityFrameworkCore;

namespace COSGeniusSystem.Services;

public class AuditService : IAuditService
{
    private readonly ApplicationDbContext _context;

    public AuditService(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<List<InventoryAuditDto>> GetAuditsAsync(CancellationToken ct = default)
    {
        await EnsureSeedAuditsAsync(ct);

        var list = await _context.InventoryAudits
            .AsNoTracking()
            .Include(a => a.Warehouse)
            .Include(a => a.AuditedByNavigation)
                .ThenInclude(u => u.UserRoles)
                    .ThenInclude(ur => ur.Role)
            .Include(a => a.AuditDetails)
                .ThenInclude(d => d.Batch)
                    .ThenInclude(b => b.Herb)
            .OrderByDescending(a => a.AuditId)
            .ThenByDescending(a => a.AuditDate)
            .ToListAsync(ct);

        return list.Select(MapToDto).ToList();
    }

    public async Task<InventoryAuditDto?> GetAuditByIdOrCodeAsync(string idOrCode, CancellationToken ct = default)
    {
        await EnsureSeedAuditsAsync(ct);

        var query = _context.InventoryAudits
            .Include(a => a.Warehouse)
            .Include(a => a.AuditedByNavigation)
                .ThenInclude(u => u.UserRoles)
                    .ThenInclude(ur => ur.Role)
            .Include(a => a.AuditDetails)
                .ThenInclude(d => d.Batch)
                    .ThenInclude(b => b.Herb)
            .AsQueryable();

        InventoryAudit? audit;
        if (int.TryParse(idOrCode, out int auditId))
        {
            audit = await query.FirstOrDefaultAsync(a => a.AuditId == auditId, ct);
        }
        else
        {
            audit = await query.FirstOrDefaultAsync(a => a.AuditCode.ToLower() == idOrCode.ToLower(), ct);
        }

        return audit == null ? null : MapToDto(audit);
    }

    public async Task<InventoryAuditDto> CreateAuditAsync(CreateAuditRequest request, int currentUserId = 0, CancellationToken ct = default)
    {
        await EnsureSeedAuditsAsync(ct);

        // Determine Auditor
        int auditorId = 0;

        // 1. If warehouse staff is creating a proposal, default to current authenticated user
        if (request.InitialStatus == "Đề xuất" && currentUserId > 0)
        {
            auditorId = currentUserId;
        }

        // 2. Check if explicitly requested and exists in DB
        if (auditorId == 0 && request.AuditedBy.HasValue && request.AuditedBy.Value > 0)
        {
            if (await _context.Users.AnyAsync(u => u.UserId == request.AuditedBy.Value, ct))
            {
                auditorId = request.AuditedBy.Value;
            }
        }

        // 3. Try to match by auditorName (FullName or Username)
        if (auditorId == 0 && !string.IsNullOrWhiteSpace(request.AuditorName))
        {
            var cleanName = request.AuditorName.Trim().ToLower();
            var matchedUser = await _context.Users
                .FirstOrDefaultAsync(u => u.FullName.ToLower() == cleanName || u.Username.ToLower() == cleanName, ct);
            if (matchedUser != null)
            {
                auditorId = matchedUser.UserId;
            }
        }

        // 4. Use current authenticated user if available
        if (auditorId == 0 && currentUserId > 0)
        {
            auditorId = currentUserId;
        }

        // 5. Fallback: find any INVENTORY_STAFF user
        if (auditorId == 0)
        {
            var staffUser = await _context.Users
                .Include(u => u.UserRoles)
                    .ThenInclude(ur => ur.Role)
                .FirstOrDefaultAsync(u => u.UserRoles.Any(r => r.Role.RoleName == "INVENTORY_STAFF"), ct);
            if (staffUser != null) auditorId = staffUser.UserId;
        }

        // 6. Ultimate fallback
        if (auditorId == 0)
        {
            var fallbackUser = await _context.Users.FirstOrDefaultAsync(ct);
            if (fallbackUser != null) auditorId = fallbackUser.UserId;
        }

        // Determine Warehouse
        int warehouseId = request.WarehouseId;
        var warehouse = await _context.Warehouses.FirstOrDefaultAsync(w => w.WarehouseId == warehouseId, ct)
            ?? await _context.Warehouses.FirstOrDefaultAsync(ct);
        if (warehouse == null)
        {
            throw new BadRequestException("Không tìm thấy kho dược liệu.");
        }

        // Generate next AuditCode: KK00009...
        var existingCodes = await _context.InventoryAudits
            .Select(a => a.AuditCode)
            .ToListAsync(ct);

        int maxNum = 8;
        foreach (var code in existingCodes)
        {
            var numStr = System.Text.RegularExpressions.Regex.Match(code, @"\d+").Value;
            if (int.TryParse(numStr, out int n) && n > maxNum)
            {
                maxNum = n;
            }
        }
        string nextCode = $"KK{maxNum + 1:D5}";

        DateTime auditDate = DateTime.UtcNow;
        if (request.ScheduledDate.HasValue)
        {
            var dt = request.ScheduledDate.Value;
            auditDate = dt.TimeOfDay == TimeSpan.Zero ? dt.Date.Add(DateTime.UtcNow.TimeOfDay) : dt;
        }

        string initialStatus = !string.IsNullOrWhiteSpace(request.InitialStatus)
            ? request.InitialStatus
            : "Chờ kiểm kê";

        string scopeStr = string.IsNullOrWhiteSpace(request.Scope) ? "Toàn kho" : request.Scope.Trim();
        string baseNote = request.Note ?? (initialStatus == "Đề xuất" ? "Đề xuất kiểm kê do Nhân viên kho tạo" : "Lịch kiểm kê do Quản lý khởi tạo");
        string finalNote = baseNote.Contains("[Phạm vi:") ? baseNote : $"[Phạm vi: {scopeStr}] {baseNote}".Trim();

        var audit = new InventoryAudit
        {
            AuditCode = nextCode,
            WarehouseId = warehouse.WarehouseId,
            AuditedBy = auditorId,
            AuditDate = auditDate,
            Status = initialStatus,
            Note = finalNote
        };

        _context.InventoryAudits.Add(audit);
        await _context.SaveChangesAsync(ct);

        // Fetch batches to snapshot
        var batchesQuery = _context.Batches
            .Include(b => b.Herb)
            .Where(b => b.WarehouseId == warehouse.WarehouseId);

        if (request.BatchIds != null && request.BatchIds.Count > 0)
        {
            batchesQuery = batchesQuery.Where(b => request.BatchIds.Contains(b.BatchId));
        }

        var batches = await batchesQuery.ToListAsync(ct);

        if (batches.Count == 0 && (request.BatchIds == null || request.BatchIds.Count == 0))
        {
            batches = await _context.Batches
                .Include(b => b.Herb)
                .ToListAsync(ct);
        }

        foreach (var batch in batches)
        {
            var detail = new AuditDetail
            {
                AuditId = audit.AuditId,
                BatchId = batch.BatchId,
                SystemQuantity = batch.CurrentQuantity,
                ActualQuantity = batch.CurrentQuantity,
                Reason = null
            };
            _context.AuditDetails.Add(detail);
        }

        await _context.SaveChangesAsync(ct);

        return (await GetAuditByIdOrCodeAsync(audit.AuditCode, ct))!;
    }

    public async Task<InventoryAuditDto> UpdateAuditStatusAsync(string idOrCode, string newStatus, string? note = null, CancellationToken ct = default)
    {
        var audit = await FindAuditEntityAsync(idOrCode, ct);
        if (audit == null)
            throw new NotFoundException($"Không tìm thấy phiếu kiểm kê {idOrCode}");

        if (audit.Status == "Đã duyệt" && (newStatus == "Đã hủy" || newStatus == "Hủy"))
            throw new BadRequestException("Không thể hủy phiếu kiểm kê đã được phê duyệt điều chỉnh tồn kho.");

        audit.Status = newStatus;
        if (!string.IsNullOrWhiteSpace(note))
        {
            audit.Note = string.IsNullOrWhiteSpace(audit.Note) ? note : $"{audit.Note} | {note}";
        }

        await _context.SaveChangesAsync(ct);
        return MapToDto(audit);
    }

    public async Task<InventoryAuditDto> UpdateAuditCountAsync(string idOrCode, UpdateAuditCountRequest request, CancellationToken ct = default)
    {
        var audit = await FindAuditEntityAsync(idOrCode, ct);
        if (audit == null)
            throw new NotFoundException($"Không tìm thấy phiếu kiểm kê {idOrCode}");

        if (audit.Status == "Đã duyệt")
            throw new BadRequestException("Phiếu kiểm kê đã được duyệt, không thể chỉnh sửa số liệu.");

        if (audit.Status == "Đã hủy")
            throw new BadRequestException("Phiếu kiểm kê đã bị hủy, không thể chỉnh sửa số liệu.");

        foreach (var item in request.Items)
        {
            var detail = audit.AuditDetails.FirstOrDefault(d => d.BatchId == item.BatchId);
            if (detail != null)
            {
                detail.ActualQuantity = item.ActualQuantity;
                if (!string.IsNullOrWhiteSpace(item.Reason))
                {
                    detail.Reason = item.Reason;
                }
            }
        }

        if (request.IsComplete)
        {
            audit.Status = "Chờ duyệt";
        }
        else
        {
            audit.Status = "Đang kiểm kê";
        }

        if (!string.IsNullOrWhiteSpace(request.Note))
        {
            audit.Note = request.Note;
        }

        await _context.SaveChangesAsync(ct);
        return MapToDto(audit);
    }

    public async Task<InventoryAuditDto> ApproveAuditAdjustmentAsync(string idOrCode, ApproveAuditRequest request, CancellationToken ct = default)
    {
        var audit = await FindAuditEntityAsync(idOrCode, ct);
        if (audit == null)
            throw new NotFoundException($"Không tìm thấy phiếu kiểm kê {idOrCode}");

        if (audit.Status == "Đã hủy")
            throw new BadRequestException("Phiếu kiểm kê đã bị hủy, không thể phê duyệt điều chỉnh tồn kho.");

        if (audit.Status == "Đã duyệt")
        {
            return MapToDto(audit);
        }

        // Synchronize actual stock with Batch.CurrentQuantity
        foreach (var detail in audit.AuditDetails)
        {
            var batch = await _context.Batches.FirstOrDefaultAsync(b => b.BatchId == detail.BatchId, ct);
            if (batch != null)
            {
                batch.CurrentQuantity = detail.ActualQuantity;
            }
        }

        audit.Status = "Đã duyệt";
        if (!string.IsNullOrWhiteSpace(request.ManagerOpinion))
        {
            audit.Note = string.IsNullOrWhiteSpace(audit.Note)
                ? $"[Quản lý duyệt]: {request.ManagerOpinion}"
                : $"{audit.Note} | [Quản lý duyệt]: {request.ManagerOpinion}";
        }

        await _context.SaveChangesAsync(ct);
        return MapToDto(audit);
    }

    private async Task<InventoryAudit?> FindAuditEntityAsync(string idOrCode, CancellationToken ct)
    {
        var query = _context.InventoryAudits
            .Include(a => a.Warehouse)
            .Include(a => a.AuditedByNavigation)
                .ThenInclude(u => u.UserRoles)
                    .ThenInclude(ur => ur.Role)
            .Include(a => a.AuditDetails)
                .ThenInclude(d => d.Batch)
                    .ThenInclude(b => b.Herb);

        if (int.TryParse(idOrCode, out int id))
        {
            return await query.FirstOrDefaultAsync(a => a.AuditId == id, ct);
        }
        return await query.FirstOrDefaultAsync(a => a.AuditCode.ToLower() == idOrCode.ToLower(), ct);
    }

    private static InventoryAuditDto MapToDto(InventoryAudit a)
    {
        var details = a.AuditDetails.Select(d =>
        {
            var disc = d.ActualQuantity - d.SystemQuantity;
            string resolution = disc == 0 ? "Khớp" : (disc < 0 ? "Điều chỉnh giảm" : "Điều chỉnh tăng");

            return new AuditDetailDto
            {
                AuditDetailId = d.AuditDetailId,
                AuditId = d.AuditId,
                BatchId = d.BatchId,
                BatchNumber = d.Batch?.BatchNumber ?? $"LO-{d.BatchId}",
                HerbId = d.Batch?.HerbId ?? 0,
                HerbCode = d.Batch?.Herb?.HerbCode ?? "VT000",
                HerbName = d.Batch?.Herb?.CommonName ?? "Vị thuốc",
                Unit = d.Batch?.Herb?.Unit ?? "kg",
                Location = a.Warehouse?.WarehouseName ?? "Kho Dược liệu trung tâm",
                SystemQuantity = d.SystemQuantity,
                ActualQuantity = d.ActualQuantity,
                Discrepancy = disc,
                Reason = d.Reason,
                ResolutionStatus = resolution
            };
        }).ToList();

        int discrepancyCount = details.Count(d => d.Discrepancy != 0);

        var rawRole = a.AuditedByNavigation?.UserRoles?.Select(ur => ur.Role.RoleName).FirstOrDefault() ?? "INVENTORY_STAFF";
        var roleLabel = rawRole switch
        {
            "ADMIN" => "Quản trị viên",
            "MANAGER" => "Quản lý phòng khám",
            "INVENTORY_STAFF" => "Nhân viên kho",
            "DOCTOR" => "Bác sĩ",
            "HERBALIST" => "Dược sĩ / Bốc thuốc",
            "CASHIER" => "Thu ngân",
            "RECEPTION_STAFF" => "Tiếp tân",
            _ => rawRole
        };

        string scope = "Toàn kho";
        if (!string.IsNullOrEmpty(a.Note) && a.Note.Contains("[Phạm vi:"))
        {
            var match = System.Text.RegularExpressions.Regex.Match(a.Note, @"\[Phạm vi:\s*([^\]]+)\]");
            if (match.Success)
            {
                scope = match.Groups[1].Value.Trim();
            }
        }

        return new InventoryAuditDto
        {
            AuditId = a.AuditId,
            AuditCode = a.AuditCode,
            WarehouseId = a.WarehouseId,
            WarehouseName = a.Warehouse?.WarehouseName ?? "Kho Dược liệu trung tâm",
            AuditedBy = a.AuditedBy,
            AuditorName = a.AuditedByNavigation?.FullName ?? $"User #{a.AuditedBy}",
            AuditorRole = roleLabel,
            AuditDate = a.AuditDate,
            Status = a.Status,
            Note = a.Note,
            Scope = scope,
            TotalItems = details.Count,
            DiscrepancyItems = discrepancyCount,
            Details = details
        };
    }

    private async Task EnsureSeedAuditsAsync(CancellationToken ct)
    {
        if (await _context.InventoryAudits.AnyAsync(ct))
            return;

        var warehouse = await _context.Warehouses.FirstOrDefaultAsync(ct);
        if (warehouse == null) return;

        var user = await _context.Users.FirstOrDefaultAsync(u => u.UserId == 1006, ct)
            ?? await _context.Users.FirstOrDefaultAsync(ct);
        if (user == null) return;

        var batches = await _context.Batches.Include(b => b.Herb).Take(4).ToListAsync(ct);
        if (batches.Count == 0) return;

        // Seed KK00008 (Chờ duyệt / Đang đếm)
        var audit8 = new InventoryAudit
        {
            AuditCode = "KK00008",
            WarehouseId = warehouse.WarehouseId,
            AuditedBy = user.UserId,
            AuditDate = new DateTime(2026, 10, 21, 8, 0, 0, DateTimeKind.Utc),
            Status = "Chờ duyệt",
            Note = "Thông tin đối chiếu thực tế"
        };
        _context.InventoryAudits.Add(audit8);
        await _context.SaveChangesAsync(ct);

        for (int i = 0; i < batches.Count; i++)
        {
            var b = batches[i];
            decimal actual = b.CurrentQuantity;
            if (i == 0) actual = Math.Max(0, b.CurrentQuantity - 0.05m); // diff -0.05
            if (i == 2) actual = b.CurrentQuantity + 0.01m; // diff +0.01

            _context.AuditDetails.Add(new AuditDetail
            {
                AuditId = audit8.AuditId,
                BatchId = b.BatchId,
                SystemQuantity = b.CurrentQuantity,
                ActualQuantity = actual,
                Reason = i == 0 ? "Hao hụt bay hơi tự nhiên" : null
            });
        }

        // Seed KK00007 (Đã duyệt)
        var audit7 = new InventoryAudit
        {
            AuditCode = "KK00007",
            WarehouseId = warehouse.WarehouseId,
            AuditedBy = user.UserId,
            AuditDate = new DateTime(2026, 10, 1, 8, 0, 0, DateTimeKind.Utc),
            Status = "Đã duyệt",
            Note = "Kiểm kê định kỳ đầu tháng 10/2026 | [Quản lý duyệt]: Đã đối soát khớp đúng thực tế"
        };
        _context.InventoryAudits.Add(audit7);
        await _context.SaveChangesAsync(ct);

        foreach (var b in batches)
        {
            _context.AuditDetails.Add(new AuditDetail
            {
                AuditId = audit7.AuditId,
                BatchId = b.BatchId,
                SystemQuantity = b.CurrentQuantity,
                ActualQuantity = b.CurrentQuantity,
                Reason = null
            });
        }

        await _context.SaveChangesAsync(ct);
    }
}


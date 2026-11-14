using System.Text.RegularExpressions;
using COSGeniusSystem.Data;
using COSGeniusSystem.DTOs;
using COSGeniusSystem.Exceptions;
using COSGeniusSystem.Models;
using Microsoft.EntityFrameworkCore;

namespace COSGeniusSystem.Services;

public class DisposalService : IDisposalService
{
    private readonly ApplicationDbContext _context;

    public DisposalService(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<List<DisposalDto>> GetDisposalsAsync(CancellationToken ct = default)
    {
        await EnsureSeedDisposalsAsync(ct);

        var list = await _context.Disposals
            .AsNoTracking()
            .Include(d => d.Warehouse)
            .Include(d => d.CreatedByNavigation)
            .Include(d => d.DisposalDetails)
                .ThenInclude(dd => dd.Batch)
                    .ThenInclude(b => b.Herb)
            .OrderByDescending(d => d.DisposalId)
            .ToListAsync(ct);

        return list.Select(MapToDto).ToList();
    }

    public async Task<DisposalDto?> GetDisposalByIdOrCodeAsync(string idOrCode, CancellationToken ct = default)
    {
        await EnsureSeedDisposalsAsync(ct);

        var isNumeric = int.TryParse(idOrCode, out int id);
        var item = await _context.Disposals
            .Include(d => d.Warehouse)
            .Include(d => d.CreatedByNavigation)
            .Include(d => d.DisposalDetails)
                .ThenInclude(dd => dd.Batch)
                    .ThenInclude(b => b.Herb)
            .FirstOrDefaultAsync(d => (isNumeric && d.DisposalId == id) || d.DisposalCode == idOrCode, ct);

        return item == null ? null : MapToDto(item);
    }

    public async Task<DisposalDto> CreateDisposalAsync(CreateDisposalRequest request, CancellationToken ct = default)
    {
        var existingCodes = await _context.Disposals
            .Select(d => d.DisposalCode)
            .ToListAsync(ct);

        int maxNum = 10;
        foreach (var c in existingCodes)
        {
            var match = Regex.Match(c, @"TH(\d+)");
            if (match.Success && int.TryParse(match.Groups[1].Value, out int num))
            {
                if (num > maxNum) maxNum = num;
            }
        }

        string nextCode = !string.IsNullOrWhiteSpace(request.DisposalCode)
            ? request.DisposalCode
            : $"TH{maxNum + 1:D5}";

        var creatorId = request.CreatedBy ?? 1;
        var entity = new Disposal
        {
            DisposalCode = nextCode,
            WarehouseId = request.WarehouseId > 0 ? request.WarehouseId : 1,
            CreatedBy = creatorId,
            DisposalDate = DateTime.Now,
            Reason = request.Reason ?? "Đề xuất tiêu hủy dược liệu",
            Status = string.IsNullOrWhiteSpace(request.Status) ? "Chờ quản lý duyệt" : request.Status
        };

        if (request.Items != null && request.Items.Count > 0)
        {
            foreach (var it in request.Items)
            {
                entity.DisposalDetails.Add(new DisposalDetail
                {
                    BatchId = it.BatchId,
                    DisposalQuantity = it.DisposalQuantity,
                    Notes = it.Notes
                });
            }
        }

        _context.Disposals.Add(entity);
        await _context.SaveChangesAsync(ct);

        return await GetDisposalByIdOrCodeAsync(entity.DisposalCode, ct)
            ?? throw new InvalidOperationException("Failed to load created disposal.");
    }

    public async Task<DisposalDto> ApproveDisposalAsync(string idOrCode, ApproveDisposalRequest request, CancellationToken ct = default)
    {
        var isNumeric = int.TryParse(idOrCode, out int id);
        var item = await _context.Disposals
            .Include(d => d.DisposalDetails)
            .FirstOrDefaultAsync(d => (isNumeric && d.DisposalId == id) || d.DisposalCode == idOrCode, ct)
            ?? throw new NotFoundException($"Disposal with code '{idOrCode}' not found.");

        item.Status = "Đã duyệt / Chờ hủy";
        var nowStr = DateTime.Now.ToString("dd/MM/yyyy · HH:mm");
        var approver = !string.IsNullOrWhiteSpace(request.ApproverName) ? request.ApproverName : "Quản lý phòng khám";
        var note = !string.IsNullOrWhiteSpace(request.ApprovalNote) ? request.ApprovalNote : "Đồng ý tiêu hủy theo số lượng kiểm kê.";

        // Append approval tag
        item.Reason = $"{StripTags(item.Reason)} [Duyệt bởi: {approver} | {nowStr} | {note}]";

        await _context.SaveChangesAsync(ct);
        return (await GetDisposalByIdOrCodeAsync(item.DisposalCode, ct))!;
    }

    public async Task<DisposalDto> RejectDisposalAsync(string idOrCode, string? reason, CancellationToken ct = default)
    {
        var isNumeric = int.TryParse(idOrCode, out int id);
        var item = await _context.Disposals
            .FirstOrDefaultAsync(d => (isNumeric && d.DisposalId == id) || d.DisposalCode == idOrCode, ct)
            ?? throw new NotFoundException($"Disposal with code '{idOrCode}' not found.");

        item.Status = "Từ chối";
        if (!string.IsNullOrWhiteSpace(reason))
        {
            item.Reason = $"{StripTags(item.Reason)} [Từ chối: {reason}]";
        }

        await _context.SaveChangesAsync(ct);
        return (await GetDisposalByIdOrCodeAsync(item.DisposalCode, ct))!;
    }

    public async Task<DisposalDto> ExecuteDisposalAsync(string idOrCode, ExecuteDisposalRequest request, CancellationToken ct = default)
    {
        var isNumeric = int.TryParse(idOrCode, out int id);
        var item = await _context.Disposals
            .Include(d => d.DisposalDetails)
                .ThenInclude(dd => dd.Batch)
            .FirstOrDefaultAsync(d => (isNumeric && d.DisposalId == id) || d.DisposalCode == idOrCode, ct)
            ?? throw new NotFoundException($"Disposal with code '{idOrCode}' not found.");

        item.Status = "Hoàn thành";
        var nowStr = DateTime.Now.ToString("dd/MM/yyyy · HH:mm");
        var executor = !string.IsNullOrWhiteSpace(request.ExecutorName) ? request.ExecutorName : "Nhân viên kho";
        var note = !string.IsNullOrWhiteSpace(request.ExecutionNote) ? request.ExecutionNote : "Đã hoàn thành tiêu hủy thực tế.";

        item.Reason = $"{StripTags(item.Reason)} [Đã tiêu hủy bởi: {executor} | {nowStr} | {note}]";

        // Deduct actual quantity from Batch
        foreach (var detail in item.DisposalDetails)
        {
            if (detail.Batch != null)
            {
                detail.Batch.CurrentQuantity = Math.Max(0m, detail.Batch.CurrentQuantity - detail.DisposalQuantity);
            }
        }

        await _context.SaveChangesAsync(ct);
        return (await GetDisposalByIdOrCodeAsync(item.DisposalCode, ct))!;
    }

    private async Task EnsureSeedDisposalsAsync(CancellationToken ct)
    {
        // Ensure TH00010 exists in database
        var hasTH10 = await _context.Disposals.AnyAsync(d => d.DisposalCode == "TH00010", ct);
        if (!hasTH10)
        {
            var th10 = new Disposal
            {
                DisposalCode = "TH00010",
                WarehouseId = 1,
                CreatedBy = 1,
                DisposalDate = DateTime.Now.AddDays(-1),
                Reason = "Đề xuất tiêu hủy tự động sinh ra sau khi duyệt cân bằng kiểm kê phiếu KK00088. [Duyệt bởi: Quan Ly (Quản lý phòng khám) | 14/11/2026 · 09:30 | Đồng ý tiêu hủy theo số lượng kiểm kê thực tế.]",
                Status = "Đã duyệt / Chờ hủy"
            };

            // Batches: Cam thảo (1: 12g, 11: 4.479g, 12: 5g), Đương quy (2: 4g, 13: 10g)
            if (await _context.Batches.AnyAsync(b => b.BatchId == 1, ct))
                th10.DisposalDetails.Add(new DisposalDetail { BatchId = 1, DisposalQuantity = 12m, Notes = "Khớp [Tiêu hủy: 12 g]" });
            if (await _context.Batches.AnyAsync(b => b.BatchId == 11, ct))
                th10.DisposalDetails.Add(new DisposalDetail { BatchId = 11, DisposalQuantity = 4.479m, Notes = "Khớp [Tiêu hủy: 4.479 g]" });
            if (await _context.Batches.AnyAsync(b => b.BatchId == 12, ct))
                th10.DisposalDetails.Add(new DisposalDetail { BatchId = 12, DisposalQuantity = 5m, Notes = "Khớp [Tiêu hủy: 5 g]" });
            if (await _context.Batches.AnyAsync(b => b.BatchId == 2, ct))
                th10.DisposalDetails.Add(new DisposalDetail { BatchId = 2, DisposalQuantity = 4m, Notes = "Khớp [Tiêu hủy: 4 g]" });
            if (await _context.Batches.AnyAsync(b => b.BatchId == 13, ct))
                th10.DisposalDetails.Add(new DisposalDetail { BatchId = 13, DisposalQuantity = 10m, Notes = "Khớp [Tiêu hủy: 10 g]" });

            _context.Disposals.Add(th10);
            await _context.SaveChangesAsync(ct);
        }

        // Ensure TH00011 exists in database
        var hasTH11 = await _context.Disposals.AnyAsync(d => d.DisposalCode == "TH00011", ct);
        if (!hasTH11)
        {
            var th11 = new Disposal
            {
                DisposalCode = "TH00011",
                WarehouseId = 1,
                CreatedBy = 1,
                DisposalDate = DateTime.Now,
                Reason = "Đề xuất tiêu hủy tự động sinh ra sau khi duyệt cân bằng kiểm kê phiếu KK00089.",
                Status = "Chờ quản lý duyệt"
            };

            // Batches: Phục linh (4: 200g), Hoàng kỳ (5: 200g)
            if (await _context.Batches.AnyAsync(b => b.BatchId == 4, ct))
                th11.DisposalDetails.Add(new DisposalDetail { BatchId = 4, DisposalQuantity = 200m, Notes = "Khớp [Tiêu hủy: 200 g]" });
            if (await _context.Batches.AnyAsync(b => b.BatchId == 5, ct))
                th11.DisposalDetails.Add(new DisposalDetail { BatchId = 5, DisposalQuantity = 200m, Notes = "Giảm tồn [Tiêu hủy: 200 g]" });

            _context.Disposals.Add(th11);
            await _context.SaveChangesAsync(ct);
        }

        // Ensure TH00009 exists in database
        var hasTH09 = await _context.Disposals.AnyAsync(d => d.DisposalCode == "TH00009", ct);
        if (!hasTH09)
        {
            var th09 = new Disposal
            {
                DisposalCode = "TH00009",
                WarehouseId = 1,
                CreatedBy = 1,
                DisposalDate = new DateTime(2026, 10, 21),
                Reason = "Vị E hết hạn",
                Status = "Chờ quản lý duyệt"
            };
            _context.Disposals.Add(th09);
            await _context.SaveChangesAsync(ct);
        }

        // Ensure TH00008 exists in database
        var hasTH08 = await _context.Disposals.AnyAsync(d => d.DisposalCode == "TH00008", ct);
        if (!hasTH08)
        {
            var th08 = new Disposal
            {
                DisposalCode = "TH00008",
                WarehouseId = 1,
                CreatedBy = 1,
                DisposalDate = new DateTime(2026, 10, 20),
                Reason = "Vị E hư hỏng [Đã tiêu hủy bởi: Trần Văn Bình | 20/10/2026 · 16:00 | Đã hoàn thành tiêu hủy thực tế.]",
                Status = "Hoàn thành"
            };
            _context.Disposals.Add(th08);
            await _context.SaveChangesAsync(ct);
        }
    }

    private static string StripTags(string? text)
    {
        if (string.IsNullOrWhiteSpace(text)) return string.Empty;
        return Regex.Replace(text, @"\[.*?\]", "").Trim();
    }

    private static DisposalDto MapToDto(Disposal entity)
    {
        string? approverName = null;
        string? approvedDate = null;
        string? approvalNote = null;
        string? executorName = null;
        string? executedDate = null;
        string? executionNote = null;

        var text = entity.Reason ?? string.Empty;
        var approveMatch = Regex.Match(text, @"\[Duyệt bởi:\s*([^\|]+)\s*\|\s*([^\|]+)\s*\|\s*([^\]]+)\]");
        if (approveMatch.Success)
        {
            approverName = approveMatch.Groups[1].Value.Trim();
            approvedDate = approveMatch.Groups[2].Value.Trim();
            approvalNote = approveMatch.Groups[3].Value.Trim();
        }

        var executeMatch = Regex.Match(text, @"\[Đã tiêu hủy bởi:\s*([^\|]+)\s*\|\s*([^\|]+)\s*\|\s*([^\]]+)\]");
        if (executeMatch.Success)
        {
            executorName = executeMatch.Groups[1].Value.Trim();
            executedDate = executeMatch.Groups[2].Value.Trim();
            executionNote = executeMatch.Groups[3].Value.Trim();
        }

        return new DisposalDto
        {
            DisposalId = entity.DisposalId,
            DisposalCode = entity.DisposalCode,
            WarehouseId = entity.WarehouseId,
            WarehouseName = entity.Warehouse?.WarehouseName ?? "Kho Dược liệu trung tâm",
            CreatedBy = entity.CreatedBy,
            CreatorName = entity.CreatedByNavigation?.FullName ?? "Nhan Vien Kho",
            DisposalDate = entity.DisposalDate,
            Reason = StripTags(entity.Reason),
            Status = entity.Status,
            ApproverName = approverName,
            ApprovedDate = approvedDate,
            ApprovalNote = approvalNote,
            ExecutorName = executorName,
            ExecutedDate = executedDate,
            ExecutionNote = executionNote,
            Details = (entity.DisposalDetails ?? new List<DisposalDetail>()).Select(d => new DisposalDetailDto
            {
                DisposalDetailId = d.DisposalDetailId,
                DisposalId = d.DisposalId,
                BatchId = d.BatchId,
                BatchNumber = d.Batch?.BatchNumber ?? $"Lô {d.BatchId}",
                HerbId = d.Batch?.HerbId ?? 0,
                HerbCode = d.Batch?.Herb?.HerbCode ?? string.Empty,
                HerbName = d.Batch?.Herb?.CommonName ?? "Dược liệu",
                Unit = d.Batch?.Herb?.Unit ?? "g",
                Location = d.Batch?.Warehouse?.WarehouseName ?? "Kho Dược liệu",
                CurrentStock = d.Batch?.CurrentQuantity ?? 0m,
                DisposalQuantity = d.DisposalQuantity,
                ExpiryDate = d.Batch?.ExpiryDate.ToString("dd/MM/yyyy"),
                Notes = d.Notes
            }).ToList()
        };
    }
}


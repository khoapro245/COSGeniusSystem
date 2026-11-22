using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using COSGeniusSystem.Data;
using COSGeniusSystem.DTOs;
using COSGeniusSystem.Exceptions;
using COSGeniusSystem.Models;
using Microsoft.EntityFrameworkCore;

namespace COSGeniusSystem.Services;

public class SupplierReturnService : ISupplierReturnService
{
    private readonly ApplicationDbContext _context;

    public SupplierReturnService(ApplicationDbContext context)
    {
        _context = context;
    }

    private static readonly HashSet<string> AllowedReasonCategories = new(StringComparer.OrdinalIgnoreCase)
    {
        "Nhà sản xuất / NCC thu hồi (Recall)",
        "Đổi trả hàng cận date theo hợp đồng nguyên tắc",
        "Khác"
    };

    public async Task<List<SupplierReturnDto>> GetSupplierReturnsAsync(CancellationToken ct = default)
    {
        var list = await _context.SupplierReturns
            .AsNoTracking()
            .Include(r => r.SupplierReturnDetails)
            .OrderByDescending(r => r.ReturnId)
            .ToListAsync(ct);

        return list.Select(MapToDto).ToList();
    }

    public async Task<SupplierReturnDto?> GetSupplierReturnByIdOrCodeAsync(string idOrCode, CancellationToken ct = default)
    {
        var isNumeric = int.TryParse(idOrCode, out int id);
        var item = await _context.SupplierReturns
            .Include(r => r.SupplierReturnDetails)
            .FirstOrDefaultAsync(r => (isNumeric && r.ReturnId == id) || r.ReturnCode == idOrCode, ct);

        return item == null ? null : MapToDto(item);
    }

    public async Task<SupplierReturnDto> CreateSupplierReturnAsync(int userId, CreateSupplierReturnRequest request, CancellationToken ct = default)
    {
        if (request.Items == null || request.Items.Count == 0)
        {
            throw new BadRequestException("Phiếu đề nghị trả nhà cung cấp phải có ít nhất 1 vị thuốc.");
        }

        // Validate reason category strictly to the 3 allowed values
        var matchedCategory = AllowedReasonCategories.FirstOrDefault(c => c.Equals(request.ReasonCategory, StringComparison.OrdinalIgnoreCase));
        if (string.IsNullOrWhiteSpace(matchedCategory))
        {
            throw new BadRequestException("Lý do trả chỉ được chọn trong 3 mục: 'Nhà sản xuất / NCC thu hồi (Recall)', 'Đổi trả hàng cận date theo hợp đồng nguyên tắc', 'Khác'.");
        }

        // Generate next ReturnCode PTxxxxx
        var existingCodes = await _context.SupplierReturns
            .Select(r => r.ReturnCode)
            .ToListAsync(ct);

        int maxNum = 3;
        foreach (var c in existingCodes)
        {
            var match = Regex.Match(c, @"\d+");
            if (match.Success && int.TryParse(match.Value, out int num))
            {
                if (num > maxNum) maxNum = num;
            }
        }
        string returnCode = request.ReturnCode?.Trim() ?? $"PT{maxNum + 1:D5}";

        decimal totalQty = request.Items.Sum(i => i.ReturnQuantity);
        decimal totalVal = request.Items.Sum(i => i.ReturnQuantity * i.UnitPrice);

        var newReturn = new SupplierReturn
        {
            ReturnCode = returnCode,
            SupplierName = request.SupplierName,
            ReceiptId = request.ReceiptId,
            ReceiptCode = request.ReceiptCode,
            BatchCode = request.BatchCode,
            WarehouseId = request.WarehouseId > 0 ? request.WarehouseId : 1,
            CreatedBy = userId > 0 ? userId : 1,
            CreatedByName = request.CreatedByName ?? "Trần Văn Bình (Thủ kho)",
            CreatedDate = DateTime.Now,
            TotalReturnQuantity = totalQty,
            TotalReturnValue = totalVal,
            ReasonCategory = matchedCategory,
            ReasonNote = request.ReasonNote ?? matchedCategory,
            VehicleNumber = request.VehicleNumber,
            ReceiverName = request.ReceiverName,
            Status = "Đề nghị trả",
            CreatedAt = DateTime.Now,
            UpdatedAt = DateTime.Now,
        };

        foreach (var it in request.Items)
        {
            DateOnly? exp = null;
            if (!string.IsNullOrWhiteSpace(it.ExpiryDate))
            {
                if (DateOnly.TryParse(it.ExpiryDate, out var d)) exp = d;
                else if (DateTime.TryParse(it.ExpiryDate, out var dt)) exp = DateOnly.FromDateTime(dt);
            }

            int? herbId = it.HerbId;
            string? herbCode = it.HerbCode;
            int? batchId = it.BatchId;

            // 1. Resolve Herb
            if ((!herbId.HasValue || string.IsNullOrWhiteSpace(herbCode)) && !string.IsNullOrWhiteSpace(it.HerbName))
            {
                var matchedHerb = await _context.Herbs.FirstOrDefaultAsync(h => h.CommonName == it.HerbName || h.HerbCode == it.HerbCode, ct);
                if (matchedHerb != null)
                {
                    herbId ??= matchedHerb.HerbId;
                    herbCode ??= matchedHerb.HerbCode;
                }
            }

            // 2. Resolve Batch
            if (!batchId.HasValue && herbId.HasValue)
            {
                if (!string.IsNullOrWhiteSpace(it.SupplierBatchCode))
                {
                    var matchedBatch = await _context.Batches.FirstOrDefaultAsync(b => b.HerbId == herbId.Value && b.SupplierBatchCode == it.SupplierBatchCode, ct);
                    if (matchedBatch != null)
                    {
                        batchId = matchedBatch.BatchId;
                    }
                }

                if (!batchId.HasValue)
                {
                    var matchedBatch = await _context.Batches
                        .Where(b => b.HerbId == herbId.Value && b.CurrentQuantity >= it.ReturnQuantity)
                        .OrderBy(b => b.ExpiryDate)
                        .FirstOrDefaultAsync(ct);
                    if (matchedBatch != null)
                    {
                        batchId = matchedBatch.BatchId;
                    }
                }
            }

            newReturn.SupplierReturnDetails.Add(new SupplierReturnDetail
            {
                BatchId = batchId,
                HerbId = herbId,
                HerbCode = herbCode,
                HerbName = it.HerbName,
                SupplierBatchCode = it.SupplierBatchCode,
                ExpiryDate = exp,
                AvailableQuantity = it.AvailableQuantity,
                ReturnQuantity = it.ReturnQuantity,
                UnitPrice = it.UnitPrice,
                LineTotal = it.ReturnQuantity * it.UnitPrice,
                Reason = it.Reason ?? matchedCategory,
                OriginalReceiptCode = it.OriginalReceiptCode ?? request.ReceiptCode,
            });
        }

        _context.SupplierReturns.Add(newReturn);
        await _context.SaveChangesAsync(ct);

        return (await GetSupplierReturnByIdOrCodeAsync(newReturn.ReturnCode, ct))!;
    }

    public async Task<SupplierReturnDto> ApproveSupplierReturnAsync(int userId, string idOrCode, ApproveSupplierReturnRequest request, CancellationToken ct = default)
    {
        var isNumeric = int.TryParse(idOrCode, out int id);
        var item = await _context.SupplierReturns
            .Include(r => r.SupplierReturnDetails)
            .FirstOrDefaultAsync(r => (isNumeric && r.ReturnId == id) || r.ReturnCode == idOrCode, ct);

        if (item == null)
        {
            throw new NotFoundException($"Không tìm thấy phiếu trả nhà cung cấp '{idOrCode}'.");
        }

        item.Status = "Đủ điều kiện bàn giao";
        item.ApprovedBy = userId > 0 ? userId : 12;
        item.ApprovedByName = request.ApproverName ?? "Quản Lý Phòng Khám";
        item.ApprovedDate = DateTime.Now;
        item.ApprovalNote = request.ApprovalNote ?? "Đã kiểm tra và phê duyệt xuất trả nhà cung cấp.";
        if (!string.IsNullOrWhiteSpace(request.VehicleNumber)) item.VehicleNumber = request.VehicleNumber.Trim();
        if (!string.IsNullOrWhiteSpace(request.ReceiverName)) item.ReceiverName = request.ReceiverName.Trim();
        item.UpdatedAt = DateTime.Now;

        await _context.SaveChangesAsync(ct);
        return MapToDto(item);
    }

    public async Task<SupplierReturnDto> RejectSupplierReturnAsync(int userId, string idOrCode, RejectSupplierReturnRequest request, CancellationToken ct = default)
    {
        var isNumeric = int.TryParse(idOrCode, out int id);
        var item = await _context.SupplierReturns
            .Include(r => r.SupplierReturnDetails)
            .FirstOrDefaultAsync(r => (isNumeric && r.ReturnId == id) || r.ReturnCode == idOrCode, ct);

        if (item == null)
        {
            throw new NotFoundException($"Không tìm thấy phiếu trả nhà cung cấp '{idOrCode}'.");
        }

        item.Status = "Từ chối";
        item.RejectionReason = request.Reason;
        item.UpdatedAt = DateTime.Now;

        await _context.SaveChangesAsync(ct);
        return MapToDto(item);
    }

    public async Task<SupplierReturnDto> HandoverSupplierReturnAsync(int userId, string idOrCode, HandoverSupplierReturnRequest request, CancellationToken ct = default)
    {
        using var transaction = await _context.Database.BeginTransactionAsync(ct);

        var isNumeric = int.TryParse(idOrCode, out int id);
        var item = await _context.SupplierReturns
            .Include(r => r.SupplierReturnDetails)
            .FirstOrDefaultAsync(r => (isNumeric && r.ReturnId == id) || r.ReturnCode == idOrCode, ct);

        if (item == null)
        {
            throw new NotFoundException($"Không tìm thấy phiếu trả nhà cung cấp '{idOrCode}'.");
        }

        if (item.Status == "Đã xuất trả")
        {
            throw new BadRequestException($"Phiếu trả '{idOrCode}' đã được xuất trả trước đó.");
        }

        if (item.Status == "Từ chối")
        {
            throw new BadRequestException($"Phiếu trả '{idOrCode}' đã bị từ chối, không thể xuất trả.");
        }

        item.Status = "Đã xuất trả";
        item.HandedOverBy = userId > 0 ? userId : 1;
        item.HandedOverByName = request.HandedOverByName ?? "Trần Văn Bình (Thủ kho)";
        item.HandedOverDate = DateTime.Now;
        item.HandoverNote = request.HandoverNote ?? "Đã bàn giao thực tế đúng số lượng cho đại diện nhà cung cấp.";
        item.UpdatedAt = DateTime.Now;

        // Trừ tồn kho thực tế của các lô thuốc tương ứng trong CSDL
        foreach (var d in item.SupplierReturnDetails)
        {
            Batch? batch = null;
            if (d.BatchId.HasValue && d.BatchId.Value > 0)
            {
                batch = await _context.Batches.FindAsync(new object[] { d.BatchId.Value }, ct);
            }

            if (batch == null)
            {
                var herbId = d.HerbId;
                if (!herbId.HasValue && !string.IsNullOrWhiteSpace(d.HerbName))
                {
                    var herb = await _context.Herbs.FirstOrDefaultAsync(h => h.CommonName == d.HerbName || h.HerbCode == d.HerbCode, ct);
                    if (herb != null)
                    {
                        herbId = herb.HerbId;
                        d.HerbId = herb.HerbId;
                        d.HerbCode = herb.HerbCode;
                    }
                }

                if (herbId.HasValue && !string.IsNullOrWhiteSpace(d.SupplierBatchCode))
                {
                    batch = await _context.Batches.FirstOrDefaultAsync(b => b.HerbId == herbId.Value && b.SupplierBatchCode == d.SupplierBatchCode, ct);
                }

                if (batch == null && herbId.HasValue)
                {
                    batch = await _context.Batches
                        .Where(b => b.HerbId == herbId.Value && b.CurrentQuantity >= d.ReturnQuantity)
                        .OrderBy(b => b.ExpiryDate)
                        .FirstOrDefaultAsync(ct);
                }

                if (batch == null && !string.IsNullOrWhiteSpace(d.SupplierBatchCode))
                {
                    batch = await _context.Batches.FirstOrDefaultAsync(b => b.SupplierBatchCode == d.SupplierBatchCode, ct);
                }
            }

            if (batch != null)
            {
                d.BatchId = batch.BatchId;
                batch.CurrentQuantity = Math.Max(0, batch.CurrentQuantity - d.ReturnQuantity);
            }
        }

        await _context.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);

        return MapToDto(item);
    }

    private static SupplierReturnDto MapToDto(SupplierReturn r)
    {
        return new SupplierReturnDto
        {
            ReturnId = r.ReturnId,
            ReturnCode = r.ReturnCode,
            SupplierName = r.SupplierName,
            ReceiptId = r.ReceiptId,
            ReceiptCode = r.ReceiptCode,
            BatchCode = r.BatchCode,
            WarehouseId = r.WarehouseId,
            CreatedBy = r.CreatedBy,
            CreatedByName = r.CreatedByName,
            CreatedDate = r.CreatedDate.ToString("dd/MM/yyyy"),
            TotalReturnQuantity = r.TotalReturnQuantity,
            TotalReturnValue = r.TotalReturnValue,
            ReasonCategory = r.ReasonCategory,
            ReasonNote = r.ReasonNote,
            Status = r.Status,
            ApprovedBy = r.ApprovedBy,
            ApprovedByName = r.ApprovedByName,
            ApprovedDate = r.ApprovedDate?.ToString("dd/MM/yyyy"),
            ApprovalNote = r.ApprovalNote,
            VehicleNumber = r.VehicleNumber,
            ReceiverName = r.ReceiverName,
            HandedOverBy = r.HandedOverBy,
            HandedOverByName = r.HandedOverByName,
            HandedOverDate = r.HandedOverDate?.ToString("dd/MM/yyyy"),
            HandoverNote = r.HandoverNote,
            RejectionReason = r.RejectionReason,
            Details = r.SupplierReturnDetails.Select(d => new SupplierReturnDetailDto
            {
                ReturnDetailId = d.ReturnDetailId,
                ReturnId = d.ReturnId,
                BatchId = d.BatchId,
                HerbId = d.HerbId,
                HerbCode = d.HerbCode,
                HerbName = d.HerbName,
                SupplierBatchCode = d.SupplierBatchCode,
                ExpiryDate = d.ExpiryDate?.ToString("dd/MM/yyyy"),
                AvailableQuantity = d.AvailableQuantity,
                ReturnQuantity = d.ReturnQuantity,
                UnitPrice = d.UnitPrice,
                LineTotal = d.LineTotal,
                Reason = d.Reason,
                OriginalReceiptCode = d.OriginalReceiptCode,
            }).ToList()
        };
    }
}


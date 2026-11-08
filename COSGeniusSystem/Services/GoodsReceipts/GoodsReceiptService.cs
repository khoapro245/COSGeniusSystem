using COSGeniusSystem.Data;
using COSGeniusSystem.DTOs;
using COSGeniusSystem.Exceptions;
using COSGeniusSystem.Models;
using Microsoft.EntityFrameworkCore;

namespace COSGeniusSystem.Services;

public class GoodsReceiptService : IGoodsReceiptService
{
    private readonly ApplicationDbContext _context;

    public GoodsReceiptService(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<List<GoodsReceiptDto>> GetGoodsReceiptsAsync(string? status = null, int? poId = null, CancellationToken ct = default)
    {
        await EnsureSeedSampleDataAsync(ct);

        var query = _context.GoodsReceipts
            .AsNoTracking()
            .Include(g => g.Po)
                .ThenInclude(p => p!.Supplier)
            .Include(g => g.Warehouse)
            .Include(g => g.ReceivedByNavigation)
            .Include(g => g.ReceiptDetails)
                .ThenInclude(d => d.Batch)
                    .ThenInclude(b => b.Herb)
            .OrderByDescending(g => g.ReceiptDate)
            .ThenByDescending(g => g.ReceiptId)
            .AsQueryable();

        if (poId.HasValue && poId.Value > 0)
        {
            query = query.Where(g => g.Poid == poId.Value);
        }

        var list = await query.ToListAsync(ct);
        var dtos = list.Select(MapToDto).ToList();

        if (!string.IsNullOrWhiteSpace(status) && !status.Equals("all", StringComparison.OrdinalIgnoreCase))
        {
            var s = status.Trim().ToLower();
            if (s == "pending" || s == "chờ duyệt" || s == "chờ quản lý duyệt")
                dtos = dtos.Where(d => d.Status.Equals("PENDING_APPROVAL", StringComparison.OrdinalIgnoreCase)).ToList();
            else if (s == "approved" || s == "đã duyệt" || s == "đã duyệt nhập")
                dtos = dtos.Where(d => d.Status.Equals("APPROVED", StringComparison.OrdinalIgnoreCase)).ToList();
            else if (s == "rejected" || s == "từ chối" || s == "từ chối duyệt")
                dtos = dtos.Where(d => d.Status.Equals("REJECTED", StringComparison.OrdinalIgnoreCase)).ToList();
        }

        return dtos;
    }

    public async Task<GoodsReceiptDto?> GetGoodsReceiptDetailAsync(string idOrCode, CancellationToken ct = default)
    {
        await EnsureSeedSampleDataAsync(ct);

        var query = _context.GoodsReceipts
            .AsNoTracking()
            .Include(g => g.Po)
                .ThenInclude(p => p!.Supplier)
            .Include(g => g.Warehouse)
            .Include(g => g.ReceivedByNavigation)
            .Include(g => g.ReceiptDetails)
                .ThenInclude(d => d.Batch)
                    .ThenInclude(b => b.Herb)
            .AsQueryable();

        GoodsReceipt? receipt;
        if (int.TryParse(idOrCode, out int receiptId))
        {
            receipt = await query.FirstOrDefaultAsync(g => g.ReceiptId == receiptId, ct);
        }
        else
        {
            receipt = await query.FirstOrDefaultAsync(g => g.ReceiptCode.ToLower() == idOrCode.ToLower(), ct);
        }

        return receipt == null ? null : MapToDto(receipt);
    }

    public async Task<GoodsReceiptDto> CreateGoodsReceiptAsync(int userId, CreateGoodsReceiptRequest request, CancellationToken ct = default)
    {
        if (request.Items == null || request.Items.Count == 0)
        {
            throw new BadRequestException("Phiếu nhập kho phải có ít nhất 1 dòng vị thuốc.");
        }

        var today = DateOnly.FromDateTime(DateTime.Today);

        // Server Validation: HSD > Ngày hiện tại, Số lượng > 0, Mã lô là bắt buộc nhập
        foreach (var item in request.Items)
        {
            if (string.IsNullOrWhiteSpace(item.SupplierBatchCode))
            {
                throw new BadRequestException("Mã lô nhà cung cấp là bắt buộc nhập cho từng dòng.");
            }

            if (item.Quantity <= 0)
            {
                throw new BadRequestException("Số lượng thực nhận phải lớn hơn 0.");
            }

            if (item.ExpiryDate <= today)
            {
                throw new BadRequestException($"Hạn sử dụng ({item.ExpiryDate:dd/MM/yyyy}) phải lớn hơn ngày hiện tại ({today:dd/MM/yyyy}).");
            }

            if (item.UnitPrice < 0)
            {
                throw new BadRequestException("Đơn giá nhập không được nhỏ hơn 0.");
            }
        }

        // Verify Warehouse
        var warehouse = await _context.Warehouses.FindAsync(new object[] { request.WarehouseId }, ct);
        if (warehouse == null)
        {
            var fallbackWh = await _context.Warehouses.FirstOrDefaultAsync(ct);
            if (fallbackWh != null)
            {
                request.WarehouseId = fallbackWh.WarehouseId;
            }
            else
            {
                var newWh = new Warehouse
                {
                    WarehouseCode = "KHO-DL01",
                    WarehouseName = "Kho Dược liệu trung tâm",
                    Location = "Tầng 1 - Khu Dược",
                    IsActive = true
                };
                _context.Warehouses.Add(newWh);
                await _context.SaveChangesAsync(ct);
                request.WarehouseId = newWh.WarehouseId;
            }
        }

        // Generate next receipt code e.g. PN00090
        var maxId = await _context.GoodsReceipts.MaxAsync(g => (int?)g.ReceiptId, ct) ?? 0;
        var receiptCode = $"PN{(maxId + 1):D5}";

        // Lookup PO info if provided
        PurchaseOrder? linkedPo = null;
        if (request.Poid.HasValue && request.Poid.Value > 0)
        {
            linkedPo = await _context.PurchaseOrders
                .Include(p => p.Podetails)
                .FirstOrDefaultAsync(p => p.Poid == request.Poid.Value, ct);
        }

        decimal totalCost = request.Items.Sum(i => i.Quantity * i.UnitPrice);

        var receipt = new GoodsReceipt
        {
            ReceiptCode = receiptCode,
            Poid = request.Poid,
            WarehouseId = request.WarehouseId,
            ReceivedBy = userId > 0 ? userId : 1,
            ReceiptDate = DateTime.Now,
            TotalCost = totalCost,
            Status = "PENDING_APPROVAL",
            Note = request.Note
        };

        int batchCounter = 1;
        foreach (var item in request.Items)
        {
            var poPrefix = linkedPo != null ? linkedPo.Pocode : "NH";
            var cleanSupplierBatch = item.SupplierBatchCode.Trim().Replace(" ", "");
            var autoBatchNumber = $"LO-{poPrefix}-{cleanSupplierBatch}-{DateTime.Now:yyMMdd}{batchCounter++}";

            // Tạo bản ghi Lô (Batch) ở trạng thái Chờ duyệt; CHƯA tăng tồn (CurrentQuantity = 0)
            var batch = new Batch
            {
                BatchNumber = autoBatchNumber,
                HerbId = item.HerbId,
                WarehouseId = request.WarehouseId,
                ImportDate = DateTime.Now,
                ExpiryDate = item.ExpiryDate,
                OriginalQuantity = item.Quantity,
                CurrentQuantity = 0, // Putaway logic: chỉ tăng tồn khi Manager duyệt
                UnitCost = item.UnitPrice,
                Status = "Chờ duyệt",
                Location = !string.IsNullOrWhiteSpace(item.Location) ? item.Location : "Kệ A - Ô01",
                SupplierBatchCode = item.SupplierBatchCode.Trim()
            };

            _context.Batches.Add(batch);

            receipt.ReceiptDetails.Add(new ReceiptDetail
            {
                Batch = batch,
                Quantity = item.Quantity,
                UnitPrice = item.UnitPrice
            });
        }

        _context.GoodsReceipts.Add(receipt);

        // [hieppm] Start: Notify to Manager (Theo sơ đồ nghiệp vụ)
        var managerUser = await _context.Users
            .FirstOrDefaultAsync(u => u.UserRoles.Any(ur => ur.Role.RoleCode == "MANAGER"), ct);
        if (managerUser != null)
        {
            _context.Notifications.Add(new Notification
            {
                UserId = managerUser.UserId,
                Title = "Phiếu nhập kho mới chờ duyệt",
                Message = $"Thủ kho đã lập phiếu nhập {receiptCode} từ đơn mua {linkedPo?.Pocode ?? "PO"}. Vui lòng kiểm tra và duyệt Putaway.",
                IsRead = false,
                CreatedAt = DateTime.Now
            });
        }
        // [hieppm] End: Notify to Manager

        await _context.SaveChangesAsync(ct);

        return (await GetGoodsReceiptDetailAsync(receipt.ReceiptId.ToString(), ct))!;
    }

    public async Task<GoodsReceiptDto> SubmitForApprovalAsync(int receiptId, CancellationToken ct = default)
    {
        var receipt = await _context.GoodsReceipts.FindAsync(new object[] { receiptId }, ct);
        if (receipt == null)
        {
            throw new NotFoundException($"Không tìm thấy phiếu nhập ID {receiptId}.");
        }

        receipt.Status = "PENDING_APPROVAL";
        await _context.SaveChangesAsync(ct);

        return (await GetGoodsReceiptDetailAsync(receiptId.ToString(), ct))!;
    }

    public async Task<GoodsReceiptDto> ApproveGoodsReceiptAsync(int managerId, int receiptId, ApproveGoodsReceiptRequest request, CancellationToken ct = default)
    {
        using var transaction = await _context.Database.BeginTransactionAsync(ct);

        var receipt = await _context.GoodsReceipts
            .Include(g => g.Po)
                .ThenInclude(p => p!.Podetails)
            .Include(g => g.ReceiptDetails)
                .ThenInclude(d => d.Batch)
            .FirstOrDefaultAsync(g => g.ReceiptId == receiptId, ct);

        if (receipt == null)
        {
            throw new NotFoundException($"Không tìm thấy phiếu nhập kho ID {receiptId}.");
        }

        if (receipt.Status.Equals("APPROVED", StringComparison.OrdinalIgnoreCase))
        {
            throw new BadRequestException($"Phiếu nhập '{receipt.ReceiptCode}' đã được phê duyệt trước đó.");
        }

        if (request.IsApproved)
        {
            // === LOGIC DUYỆT (PUTAWAY): CỘNG SỐ LƯỢNG THUỐC VÀO KHO LƯU TRỮ THEO TỪNG LÔ ===
            receipt.Status = "APPROVED";
            if (!string.IsNullOrWhiteSpace(request.ManagerNote))
            {
                receipt.Note = string.IsNullOrWhiteSpace(receipt.Note)
                    ? $"[Ý kiến duyệt]: {request.ManagerNote}"
                    : $"{receipt.Note} | [Ý kiến duyệt]: {request.ManagerNote}";
            }

            foreach (var detail in receipt.ReceiptDetails)
            {
                if (detail.Batch != null)
                {
                    // Tăng tồn kho thực tế cho lô hàng
                    detail.Batch.CurrentQuantity = detail.Quantity;
                    detail.Batch.OriginalQuantity = detail.Quantity;
                    detail.Batch.Status = "Đạt chuẩn";
                }
            }

            // Cập nhật trạng thái của PO liên kết (nếu có)
            if (receipt.Po != null)
            {
                // Lấy tất cả các phiếu nhập đã duyệt của PO này (bao gồm cả phiếu hiện tại)
                var allApprovedDetails = await _context.ReceiptDetails
                    .Include(rd => rd.Batch)
                    .Where(rd => rd.Receipt.Poid == receipt.Po.Poid &&
                                 (rd.Receipt.Status == "APPROVED" || rd.ReceiptId == receipt.ReceiptId))
                    .ToListAsync(ct);

                bool allCompleted = true;
                bool anyReceived = false;

                foreach (var poItem in receipt.Po.Podetails)
                {
                    decimal totalReceivedForHerb = allApprovedDetails
                        .Where(rd => rd.Batch != null && rd.Batch.HerbId == poItem.HerbId)
                        .Sum(rd => rd.Quantity);

                    if (totalReceivedForHerb > 0)
                    {
                        anyReceived = true;
                    }

                    if (totalReceivedForHerb < poItem.OrderQuantity)
                    {
                        allCompleted = false;
                    }
                }

                if (allCompleted && receipt.Po.Podetails.Count > 0)
                {
                    receipt.Po.Status = "Hoàn thành";
                }
                else if (anyReceived)
                {
                    receipt.Po.Status = "Nhận 1 phần";
                }
            }
        }
        else
        {
            // === LOGIC TỪ CHỐI ===
            receipt.Status = "REJECTED";
            if (!string.IsNullOrWhiteSpace(request.ManagerNote))
            {
                receipt.Note = string.IsNullOrWhiteSpace(receipt.Note)
                    ? $"[Từ chối duyệt]: {request.ManagerNote}"
                    : $"{receipt.Note} | [Từ chối]: {request.ManagerNote}";
            }

            foreach (var detail in receipt.ReceiptDetails)
            {
                if (detail.Batch != null)
                {
                    detail.Batch.CurrentQuantity = 0;
                    detail.Batch.Status = "Từ chối";
                }
            }
        }

        // [hieppm] Start: Notify to Manager (Ghi nhận thông báo hoàn tất Putaway)
        var managerToNotify = await _context.Users
            .FirstOrDefaultAsync(u => u.UserRoles.Any(ur => ur.Role.RoleCode == "MANAGER"), ct);
        if (managerToNotify != null)
        {
            _context.Notifications.Add(new Notification
            {
                UserId = managerToNotify.UserId,
                Title = request.IsApproved ? "Phiếu nhập kho đã hoàn tất Putaway" : "Phiếu nhập kho bị từ chối",
                Message = request.IsApproved
                    ? $"Phiếu nhập {receipt.ReceiptCode} đã được phê duyệt tăng tồn kho và cập nhật trạng thái đơn PO."
                    : $"Phiếu nhập {receipt.ReceiptCode} đã bị từ chối phê duyệt.",
                IsRead = false,
                CreatedAt = DateTime.Now
            });
        }
        // [hieppm] End: Notify to Manager

        await _context.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);

        return (await GetGoodsReceiptDetailAsync(receiptId.ToString(), ct))!;
    }

    private static GoodsReceiptDto MapToDto(GoodsReceipt receipt)
    {
        string statusLabel = receipt.Status switch
        {
            "APPROVED" => "Đã duyệt nhập",
            "REJECTED" => "Từ chối duyệt",
            _ => "Chờ quản lý duyệt"
        };

        var details = receipt.ReceiptDetails.Select(d => new ReceiptDetailDto
        {
            ReceiptDetailId = d.ReceiptDetailId,
            ReceiptId = d.ReceiptId,
            BatchId = d.BatchId,
            BatchNumber = d.Batch?.BatchNumber ?? string.Empty,
            HerbId = d.Batch?.HerbId ?? 0,
            HerbCode = d.Batch?.Herb?.HerbCode ?? $"VT{d.Batch?.HerbId:D5}",
            HerbName = d.Batch?.Herb?.CommonName ?? "Vị thuốc",
            SupplierBatchCode = d.Batch?.SupplierBatchCode ?? d.Batch?.BatchNumber,
            ExpiryDate = d.Batch?.ExpiryDate ?? DateOnly.FromDateTime(DateTime.Today.AddYears(1)),
            Quantity = d.Quantity,
            UnitPrice = d.UnitPrice,
            LineTotal = d.Quantity * d.UnitPrice,
            Location = d.Batch?.Location ?? "Kho Dược liệu"
        }).ToList();

        decimal totalQty = details.Sum(d => d.Quantity);

        return new GoodsReceiptDto
        {
            ReceiptId = receipt.ReceiptId,
            ReceiptCode = receipt.ReceiptCode,
            Poid = receipt.Poid,
            Pocode = receipt.Po?.Pocode,
            SupplierName = receipt.Po?.Supplier?.SupplierName ?? "Nhà cung cấp",
            WarehouseId = receipt.WarehouseId,
            WarehouseName = receipt.Warehouse?.WarehouseName ?? "Kho Dược liệu trung tâm",
            ReceivedBy = receipt.ReceivedBy,
            ReceiverName = receipt.ReceivedByNavigation?.FullName ?? "Nhân viên kho",
            ReceiptDate = receipt.ReceiptDate,
            TotalCost = receipt.TotalCost,
            TotalQuantity = totalQty,
            Status = receipt.Status,
            StatusLabel = statusLabel,
            Note = receipt.Note,
            ProcessLog = receipt.Status == "APPROVED"
                ? $"{receipt.ReceiptDate:dd/MM/yyyy HH:mm} · Quản lý đã duyệt nhập kho và tăng tồn theo lô."
                : $"{receipt.ReceiptDate:dd/MM/yyyy HH:mm} · {receipt.ReceivedByNavigation?.FullName ?? "Nhân viên kho"} gửi phiếu chờ quản lý duyệt.\nChưa tăng tồn. Sau duyệt xem lô hàng và giao dịch nhập.",
            Details = details
        };
    }

    private async Task EnsureSeedSampleDataAsync(CancellationToken ct)
    {
        if (await _context.GoodsReceipts.AnyAsync(ct))
        {
            return;
        }

        var po = await _context.PurchaseOrders
            .Include(p => p.Supplier)
            .Include(p => p.Podetails)
                .ThenInclude(d => d.Herb)
            .FirstOrDefaultAsync(ct);

        if (po == null)
        {
            return;
        }

        var warehouse = await _context.Warehouses.FirstOrDefaultAsync(ct);
        int warehouseId = warehouse?.WarehouseId ?? 1;
        var user = await _context.Users.FirstOrDefaultAsync(ct);
        int receiverId = user?.UserId ?? 1;

        // Seed PN00089 (Đã duyệt)
        var receipt89 = new GoodsReceipt
        {
            ReceiptCode = "PN00089",
            Poid = po.Poid,
            WarehouseId = warehouseId,
            ReceivedBy = receiverId,
            ReceiptDate = DateTime.Now.AddDays(-2),
            TotalCost = 3500000,
            Status = "APPROVED",
            Note = "Nhập đủ hàng theo đợt 1."
        };

        var firstHerb = po.Podetails.FirstOrDefault()?.Herb;
        if (firstHerb != null)
        {
            var b1 = new Batch
            {
                BatchNumber = "LA26001",
                HerbId = firstHerb.HerbId,
                WarehouseId = warehouseId,
                ImportDate = DateTime.Now.AddDays(-2),
                ExpiryDate = DateOnly.FromDateTime(DateTime.Today.AddYears(1)),
                OriginalQuantity = 5,
                CurrentQuantity = 5,
                UnitCost = 400000,
                Status = "Đạt chuẩn",
                Location = "Kệ A - Ô01",
                SupplierBatchCode = "NCC-A01"
            };
            _context.Batches.Add(b1);
            receipt89.ReceiptDetails.Add(new ReceiptDetail { Batch = b1, Quantity = 5, UnitPrice = 400000 });
        }

        // Seed PN00090 (Chờ duyệt)
        var receipt90 = new GoodsReceipt
        {
            ReceiptCode = "PN00090",
            Poid = po.Poid,
            WarehouseId = warehouseId,
            ReceivedBy = receiverId,
            ReceiptDate = DateTime.Now,
            TotalCost = 2600000,
            Status = "PENDING_APPROVAL",
            Note = "Thông tin thực giao, bao bì hoặc điều kiện đã thỏa thuận"
        };

        if (po.Podetails.Count > 1)
        {
            var secondHerb = po.Podetails.Skip(1).First().Herb;
            var b2 = new Batch
            {
                BatchNumber = "LB26002",
                HerbId = secondHerb.HerbId,
                WarehouseId = warehouseId,
                ImportDate = DateTime.Now,
                ExpiryDate = DateOnly.FromDateTime(DateTime.Today.AddMonths(18)),
                OriginalQuantity = 8,
                CurrentQuantity = 0, // Chờ duyệt -> chưa tăng tồn
                UnitCost = 300000,
                Status = "Chờ duyệt",
                Location = "Kệ B - Ô02",
                SupplierBatchCode = "NCC-B02"
            };
            _context.Batches.Add(b2);
            receipt90.ReceiptDetails.Add(new ReceiptDetail { Batch = b2, Quantity = 8, UnitPrice = 300000 });
        }

        _context.GoodsReceipts.AddRange(receipt89, receipt90);
        await _context.SaveChangesAsync(ct);
    }
}


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
        var query = _context.GoodsReceipts
            .AsNoTracking()
            .Include(g => g.Po)
                .ThenInclude(p => p!.Supplier)
            .Include(g => g.Warehouse)
            .Include(g => g.ReceivedByNavigation)
            .Include(g => g.ReceiptDetails)
                .ThenInclude(d => d.Batch)
                    .ThenInclude(b => b.Herb)
            // [hieppm] Start: Nạp thông tin kệ lưu trữ StorageLocation
            .Include(g => g.ReceiptDetails)
                .ThenInclude(d => d.Batch)
                    .ThenInclude(b => b.StorageLocation)
            // [hieppm] End: Nạp thông tin kệ lưu trữ StorageLocation
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
                dtos = dtos.Where(d => d.Status.Equals(GoodsReceiptStatusConstants.PendingApproval, StringComparison.OrdinalIgnoreCase) || d.Status.Equals("Chờ duyệt", StringComparison.OrdinalIgnoreCase)).ToList();
            else if (s == "approved" || s == "đã duyệt" || s == "đã duyệt nhập")
                dtos = dtos.Where(d => d.Status.Equals(GoodsReceiptStatusConstants.Approved, StringComparison.OrdinalIgnoreCase) || d.Status.Equals("Đã duyệt", StringComparison.OrdinalIgnoreCase)).ToList();
            else if (s == "rejected" || s == "từ chối" || s == "từ chối duyệt")
                dtos = dtos.Where(d => d.Status.Equals(GoodsReceiptStatusConstants.Rejected, StringComparison.OrdinalIgnoreCase) || d.Status.Equals("Từ chối", StringComparison.OrdinalIgnoreCase)).ToList();
        }

        return dtos;
    }

    public async Task<GoodsReceiptDto?> GetGoodsReceiptDetailAsync(string idOrCode, CancellationToken ct = default)
    {
        var query = _context.GoodsReceipts
            .AsNoTracking()
            .Include(g => g.Po)
                .ThenInclude(p => p!.Supplier)
            .Include(g => g.Warehouse)
            .Include(g => g.ReceivedByNavigation)
            .Include(g => g.ReceiptDetails)
                .ThenInclude(d => d.Batch)
                    .ThenInclude(b => b.Herb)
            // [hieppm] Start: Nạp thông tin kệ lưu trữ StorageLocation
            .Include(g => g.ReceiptDetails)
                .ThenInclude(d => d.Batch)
                    .ThenInclude(b => b.StorageLocation)
            // [hieppm] End: Nạp thông tin kệ lưu trữ StorageLocation
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
            throw new BadRequestException("A goods receipt must contain at least one herb line.");
        }

        if (!request.Poid.HasValue || request.Poid.Value <= 0)
        {
            throw new BadRequestException(
                "A purchase order is required to create a goods receipt.");
        }

        var today = DateOnly.FromDateTime(DateTime.Today);

        // Server Validation: HSD > Ngày hiện tại, Số lượng > 0, Mã lô là bắt buộc nhập
        foreach (var item in request.Items)
        {
            if (string.IsNullOrWhiteSpace(item.SupplierBatchCode))
            {
                throw new BadRequestException("A supplier batch code is required for each line.");
            }

            if (item.Quantity <= 0)
            {
                throw new BadRequestException("The actual received quantity must be greater than 0.");
            }

            if (item.ExpiryDate <= today)
            {
                throw new BadRequestException($"The expiry date ({item.ExpiryDate:dd/MM/yyyy}) must be after today ({today:dd/MM/yyyy}).");
            }

            if (item.UnitPrice < 0)
            {
                throw new BadRequestException("The receipt unit price must not be negative.");
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

        // [hieppm] Start: Sinh ReceiptCode duy nhất an toàn, không trùng lặp
        var existingCodes = await _context.GoodsReceipts
            .Where(g => g.ReceiptCode.StartsWith("PN"))
            .Select(g => g.ReceiptCode)
            .ToListAsync(ct);

        int nextSeq = 1;
        foreach (var code in existingCodes)
        {
            if (code.Length > 2)
            {
                var numPart = code.Substring(2);
                if (int.TryParse(numPart, out int parsedSeq) && parsedSeq >= nextSeq)
                {
                    nextSeq = parsedSeq + 1;
                }
            }
        }
        var receiptCode = $"PN{nextSeq:D5}";
        while (await _context.GoodsReceipts.AnyAsync(g => g.ReceiptCode == receiptCode, ct))
        {
            nextSeq++;
            receiptCode = $"PN{nextSeq:D5}";
        }
        // [hieppm] End: Sinh ReceiptCode duy nhất an toàn

        // Lookup PO info if provided
        PurchaseOrder? linkedPo = null;
        if (request.Poid.HasValue && request.Poid.Value > 0)
        {
            linkedPo = await _context.PurchaseOrders
                .Include(p => p.Podetails)
                .FirstOrDefaultAsync(p => p.Poid == request.Poid.Value, ct);

            // [hieppm] Start: Không cho phép lập phiếu nhập từ đơn PO chưa phát hành (Nháp)
            if (linkedPo == null)
            {
                throw new NotFoundException($"Purchase order ID {request.Poid.Value} was not found.");
            }
            var poStatusLower = (linkedPo.Status ?? string.Empty).Trim().ToLower();
            if (poStatusLower == "nháp" || poStatusLower == "draft" || poStatusLower == "mới tạo")
            {
                throw new BadRequestException($"Purchase order '{linkedPo.Pocode}' has status '{linkedPo.Status}' and has not been issued, so a goods receipt cannot be created.");
            }
            // [hieppm] End: Không cho phép lập phiếu nhập từ đơn PO chưa phát hành (Nháp)
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
            // [hieppm] Start: Khởi tạo trạng thái mặc định của phiếu nhập kho theo Database Constraint DF_GoodsReceipt_Status
            Status = GoodsReceiptStatusConstants.PendingApproval,
            // [hieppm] End: Khởi tạo trạng thái mặc định của phiếu nhập kho theo Database Constraint DF_GoodsReceipt_Status
            Note = request.Note,
            // [hieppm] Start: Bổ sung DriverName, VehicleNumber, DocumentUrl
            DriverName = request.DriverName,
            VehicleNumber = request.VehicleNumber,
            DocumentUrl = request.DocumentUrl
            // [hieppm] End
        };

        // [hieppm] Start: Lấy vị trí kho từ database nếu không nhập vị trí cụ thể
        var targetWarehouse = await _context.Warehouses.FindAsync(new object[] { request.WarehouseId }, ct);
        var defaultWarehouseLocation = !string.IsNullOrWhiteSpace(targetWarehouse?.Location)
            ? targetWarehouse.Location
            : (targetWarehouse?.WarehouseName ?? "Chưa phân vị trí");
        // [hieppm] End: Lấy vị trí kho từ database

        // [hieppm] Start: Sinh mã BatchNumber duy nhất, chống xung đột khóa UNIQUE constraint khi nhập lại PO bị từ chối
        var usedBatchNumbers = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        int batchCounter = 1;
        foreach (var item in request.Items)
        {
            var poPrefix = linkedPo != null ? linkedPo.Pocode : "NH";
            var cleanSupplierBatch = string.IsNullOrWhiteSpace(item.SupplierBatchCode) ? "BATCH" : item.SupplierBatchCode.Trim().Replace(" ", "");
            if (cleanSupplierBatch.Length > 16)
            {
                cleanSupplierBatch = cleanSupplierBatch.Substring(0, 16);
            }

            var timeTag = DateTime.Now.ToString("yyMMddHHmm");
            var baseBatch = $"LO-{poPrefix}-{cleanSupplierBatch}-{timeTag}";
            if (baseBatch.Length > 44)
            {
                baseBatch = baseBatch.Substring(0, 44);
            }

            int subIndex = batchCounter++;
            string autoBatchNumber = $"{baseBatch}{subIndex}";
            if (autoBatchNumber.Length > 50)
            {
                autoBatchNumber = autoBatchNumber.Substring(0, 50);
            }

            while (usedBatchNumbers.Contains(autoBatchNumber) || await _context.Batches.AnyAsync(b => b.BatchNumber == autoBatchNumber, ct))
            {
                subIndex++;
                autoBatchNumber = $"{baseBatch}{subIndex}";
                if (autoBatchNumber.Length > 50)
                {
                    autoBatchNumber = autoBatchNumber.Substring(0, 50);
                }
            }
            usedBatchNumbers.Add(autoBatchNumber);
            // [hieppm] End: Sinh mã BatchNumber duy nhất

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
                // [hieppm] Start: Gắn StorageLocationId cho Lô hàng
                StorageLocationId = item.StorageLocationId,
                Location = !string.IsNullOrWhiteSpace(item.Location) ? item.Location.Trim() : defaultWarehouseLocation,
                // [hieppm] End: Gắn StorageLocationId cho Lô hàng
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
            throw new NotFoundException($"Goods receipt ID {receiptId} was not found.");
        }

        var status = receipt.Status?.Trim();

        if (string.Equals(status, GoodsReceiptStatusConstants.Approved,
                StringComparison.OrdinalIgnoreCase)
            || string.Equals(status, "Đã duyệt",
                StringComparison.OrdinalIgnoreCase)
            || string.Equals(status, "Đã duyệt nhập",
                StringComparison.OrdinalIgnoreCase))
        {
            throw new BadRequestException(
                $"Goods receipt '{receipt.ReceiptCode}' has already been approved and cannot be resubmitted.");
        }

        receipt.Status = GoodsReceiptStatusConstants.PendingApproval;
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
            throw new NotFoundException($"Goods receipt ID {receiptId} was not found.");
        }

        if (receipt.Status.Equals(GoodsReceiptStatusConstants.Approved, StringComparison.OrdinalIgnoreCase) ||
            receipt.Status.Equals("Đã duyệt", StringComparison.OrdinalIgnoreCase))
        {
            throw new BadRequestException($"Goods receipt '{receipt.ReceiptCode}' has already been approved.");
        }

        if (request.IsApproved)
        {
            // === LOGIC DUYỆT (PUTAWAY): CỘNG SỐ LƯỢNG THUỐC VÀO KHO LƯU TRỮ THEO TỪNG LÔ ===
            receipt.Status = GoodsReceiptStatusConstants.Approved;
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
                    // [hieppm] Start: Logic cộng dồn số lượng vào kho (Số lượng hiện có trong kho + Số lượng nhập từ PO, tuyệt đối KHÔNG thay thế)
                    var previousBatchQuantity = detail.Batch.CurrentQuantity;
                    var previousOriginalQuantity = detail.Batch.OriginalQuantity;

                    // Tăng tồn kho thực tế cho lô hàng: Cộng dồn số lượng nhập từ PO vào số lượng hiện có trong kho
                    detail.Batch.CurrentQuantity = previousBatchQuantity + detail.Quantity;
                    if (previousOriginalQuantity <= 0)
                    {
                        detail.Batch.OriginalQuantity = detail.Quantity;
                    }
                    detail.Batch.Status = "Đạt chuẩn";

                    // Tự động cập nhật vị trí và đơn giá nếu Quản lý điều chỉnh lúc duyệt
                    if (request.Items != null && request.Items.Count > 0)
                    {
                        var updateItem = request.Items.FirstOrDefault(i =>
                            (i.BatchId.HasValue && i.BatchId.Value == detail.Batch.BatchId) ||
                            (!string.IsNullOrWhiteSpace(i.SupplierBatchCode) && i.SupplierBatchCode.Equals(detail.Batch.SupplierBatchCode, StringComparison.OrdinalIgnoreCase)) ||
                            (i.HerbId.HasValue && i.HerbId.Value == detail.Batch.HerbId)
                        );

                        if (updateItem != null)
                        {
                            if (updateItem.UnitPrice.HasValue && updateItem.UnitPrice.Value >= 0)
                            {
                                detail.UnitPrice = updateItem.UnitPrice.Value;
                                detail.Batch.UnitCost = updateItem.UnitPrice.Value;
                            }
                            if (!string.IsNullOrWhiteSpace(updateItem.Location))
                            {
                                detail.Batch.Location = updateItem.Location.Trim();
                            }
                            if (updateItem.StorageLocationId.HasValue && updateItem.StorageLocationId.Value > 0)
                            {
                                detail.Batch.StorageLocationId = updateItem.StorageLocationId.Value;
                            }
                        }
                    }

                    // Đảm bảo vị trí lưu kho của lô hàng không bị rỗng
                    if (string.IsNullOrWhiteSpace(detail.Batch.Location))
                    {
                        detail.Batch.Location = "Kho Dược liệu trung tâm";
                    }

                    // Tính số dư tồn kho sau khi cộng dồn (Số lượng đang có trong kho + Số lượng nhập từ PO)
                    var otherBatchesTotal = await _context.Batches
                        .Where(b => b.HerbId == detail.Batch.HerbId && b.Status == "Đạt chuẩn" && b.BatchId != detail.Batch.BatchId)
                        .SumAsync(b => (decimal?)b.CurrentQuantity, ct) ?? 0m;
                    var totalHerbStockAfter = otherBatchesTotal + detail.Batch.CurrentQuantity;

                    // Ghi nhận biến động tồn kho vào bảng ReceiptHistory:
                    // QuantityChanged: Số lượng nhập từ PO
                    // BalanceAfter: Tổng số lượng trong kho sau khi cộng dồn (= Số lượng cũ trong kho + Số lượng nhập từ PO)
                    _context.ReceiptHistories.Add(new ReceiptHistory
                    {
                        BatchId = detail.Batch.BatchId,
                        ReceiptId = receipt.ReceiptId,
                        QuantityChanged = detail.Quantity,
                        BalanceAfter = totalHerbStockAfter,
                        ChangeDate = DateTime.Now
                    });

                    // Cập nhật giá nhập gần nhất (LastImportPrice) cho vị thuốc tương ứng khi hoàn tất nhập kho
                    var herb = await _context.Herbs.FindAsync(new object[] { detail.Batch.HerbId }, ct);
                    if (herb != null && detail.UnitPrice > 0)
                    {
                        herb.LastImportPrice = detail.UnitPrice;
                    }
                    // [hieppm] End: Logic cộng dồn số lượng vào kho
                }
            }

            // Cập nhật trạng thái của PO liên kết (nếu có)
            if (receipt.Po != null)
            {
                // Lấy tất cả các phiếu nhập đã duyệt của PO này (bao gồm cả phiếu hiện tại)
                // [hieppm] Start: Đồng bộ kiểm tra các biến thể trạng thái đã duyệt của phiếu nhập
                var allApprovedDetails = await _context.ReceiptDetails
                    .Include(rd => rd.Batch)
                    .Where(rd => rd.Receipt.Poid == receipt.Po.Poid &&
                                 (rd.Receipt.Status == GoodsReceiptStatusConstants.Approved ||
                                  rd.Receipt.Status == "Đã duyệt" ||
                                  rd.Receipt.Status == "Đã duyệt nhập" ||
                                  rd.ReceiptId == receipt.ReceiptId))
                    .ToListAsync(ct);
                // [hieppm] End: Đồng bộ kiểm tra các biến thể trạng thái đã duyệt của phiếu nhập

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
            receipt.Status = GoodsReceiptStatusConstants.Rejected;
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

            // [hieppm] Start: Đồng bộ trạng thái PO khi phiếu nhập bị từ chối, giúp nhân viên kho có thể nhập lại
            if (receipt.Po != null)
            {
                var hasAnyApprovedReceipt = await _context.GoodsReceipts
                    .AnyAsync(g => g.Poid == receipt.Po.Poid &&
                                   g.ReceiptId != receipt.ReceiptId &&
                                   (g.Status == GoodsReceiptStatusConstants.Approved ||
                                    g.Status == "Đã duyệt" ||
                                    g.Status == "Đã duyệt nhập"), ct);

                receipt.Po.Status = hasAnyApprovedReceipt ? "Nhận 1 phần" : "Chờ nhận";
            }
            // [hieppm] End: Đồng bộ trạng thái PO khi phiếu nhập bị từ chối
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

        // [hieppm] Start: Thông báo cho Thủ kho khi phiếu bị Quản lý từ chối
        if (!request.IsApproved && receipt.ReceivedBy > 0)
        {
            var staffToNotify = await _context.Users.FindAsync(new object[] { receipt.ReceivedBy }, ct);
            if (staffToNotify != null)
            {
                _context.Notifications.Add(new Notification
                {
                    UserId = staffToNotify.UserId,
                    Title = $"Phiếu nhập kho {receipt.ReceiptCode} bị từ chối",
                    Message = $"Phiếu nhập {receipt.ReceiptCode} (Đơn {receipt.Po?.Pocode ?? "PO"}) đã bị Quản lý từ chối. Lý do: {request.ManagerNote ?? "Không đạt chuẩn"}. Bạn có thể kiểm tra và tiến hành nhập lại cho đơn mua hàng này.",
                    IsRead = false,
                    CreatedAt = DateTime.Now
                });
            }
        }
        // [hieppm] End: Thông báo cho Thủ kho

        await _context.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);

        return (await GetGoodsReceiptDetailAsync(receiptId.ToString(), ct))!;
    }

    private static GoodsReceiptDto MapToDto(GoodsReceipt receipt)
    {
        var rawStatus = (receipt.Status ?? string.Empty).Trim().ToUpper();
        string statusLabel = rawStatus switch
        {
            GoodsReceiptStatusConstants.Approved or "ĐÃ DUYỆT" or "ĐÃ DUYỆT NHẬP" => "Đã duyệt nhập",
            GoodsReceiptStatusConstants.Rejected or "TỪ CHỐI" or "TỪ CHỐI DUYỆT" => "Từ chối duyệt",
            _ => "Chờ quản lý duyệt"
        };

        // [hieppm] Start: Lấy batchCode và location từ Database
        var details = receipt.ReceiptDetails.Select(d => new ReceiptDetailDto
        {
            ReceiptDetailId = d.ReceiptDetailId,
            ReceiptId = d.ReceiptId,
            BatchId = d.BatchId,
            BatchNumber = d.Batch?.BatchNumber ?? string.Empty,
            HerbId = d.Batch?.HerbId ?? 0,
            HerbCode = d.Batch?.Herb?.HerbCode ?? $"VT{d.Batch?.HerbId:D5}",
            HerbName = d.Batch?.Herb?.CommonName ?? "Vị thuốc",
            SupplierBatchCode = !string.IsNullOrWhiteSpace(d.Batch?.SupplierBatchCode) ? d.Batch.SupplierBatchCode : d.Batch?.BatchNumber,
            ExpiryDate = d.Batch?.ExpiryDate ?? DateOnly.FromDateTime(DateTime.Today.AddYears(1)),
            Quantity = d.Quantity,
            UnitPrice = d.UnitPrice,
            LineTotal = d.Quantity * d.UnitPrice,
            // [hieppm] Start: Trả về StorageLocationId và Code
            StorageLocationId = d.Batch?.StorageLocationId,
            StorageLocationCode = d.Batch?.StorageLocation?.Code,
            Location = d.Batch?.StorageLocation?.Code ?? (!string.IsNullOrWhiteSpace(d.Batch?.Location)
                ? d.Batch.Location
                : (receipt.Warehouse?.Location ?? receipt.Warehouse?.WarehouseName ?? "Chưa phân vị trí"))
            // [hieppm] End
        }).ToList();
        // [hieppm] End: Lấy batchCode và location từ Database

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
            // [hieppm] Start: Map DriverName, VehicleNumber, DocumentUrl
            DriverName = receipt.DriverName,
            VehicleNumber = receipt.VehicleNumber,
            DocumentUrl = receipt.DocumentUrl,
            // [hieppm] End
            ProcessLog = rawStatus == GoodsReceiptStatusConstants.Approved || rawStatus == "ĐÃ DUYỆT"
                ? $"{receipt.ReceiptDate:dd/MM/yyyy HH:mm} · Quản lý đã duyệt nhập kho và tăng tồn theo lô."
                : $"{receipt.ReceiptDate:dd/MM/yyyy HH:mm} · {receipt.ReceivedByNavigation?.FullName ?? "Nhân viên kho"} gửi phiếu chờ quản lý duyệt.\nChưa tăng tồn. Sau duyệt xem lô hàng và giao dịch nhập.",
            Details = details
        };
    }

    // [hieppm] Start: Triển khai gợi ý kệ kho theo vị thuốc và khối lượng
    public async Task<List<StorageLocationSuggestionDto>> GetSuggestedStorageLocationsAsync(
        int? herbId, decimal? requiredGrams, int warehouseId = 1, CancellationToken ct = default)
    {
        // 1. Xác định điều kiện bảo quản của dược liệu nếu có herbId
        bool isColdRequired = false;
        if (herbId.HasValue && herbId.Value > 0)
        {
            var herb = await _context.Herbs.FindAsync(new object[] { herbId.Value }, ct);
            if (herb != null)
            {
                isColdRequired = herb.IsColdStorageRequired;
            }
        }

        string targetStorageType = isColdRequired ? "COLD" : "STANDARD";

        // 2. Lấy danh sách kệ đang active thuộc kho
        var locations = await _context.WarehouseStorageLocations
            .AsNoTracking()
            .Where(l => l.WarehouseId == warehouseId && l.IsActive)
            .ToListAsync(ct);

        // 3. Tính toán dung tích đã sử dụng cho từng kệ từ các Batch đang tồn tại (CurrentQuantity > 0)
        var usedCapacities = await _context.Batches
            .AsNoTracking()
            .Where(b => b.WarehouseId == warehouseId && b.StorageLocationId != null && b.CurrentQuantity > 0)
            .GroupBy(b => b.StorageLocationId!.Value)
            .Select(g => new { LocationId = g.Key, Used = g.Sum(b => b.CurrentQuantity) })
            .ToDictionaryAsync(x => x.LocationId, x => x.Used, ct);

        var result = new List<StorageLocationSuggestionDto>();
        foreach (var loc in locations)
        {
            decimal used = usedCapacities.TryGetValue(loc.Id, out var u) ? u : 0;
            decimal remaining = Math.Max(0, loc.MaxCapacityGrams - used);
            bool typeMatches = string.Equals(loc.StorageType, targetStorageType, StringComparison.OrdinalIgnoreCase);
            bool hasEnoughCapacity = !requiredGrams.HasValue || remaining >= requiredGrams.Value;

            result.Add(new StorageLocationSuggestionDto
            {
                Id = loc.Id,
                Code = loc.Code,
                StorageType = loc.StorageType,
                MaxCapacityGrams = loc.MaxCapacityGrams,
                CurrentUsedGrams = used,
                RemainingCapacityGrams = remaining,
                IsSuitable = typeMatches && hasEnoughCapacity
            });
        }

        // Ưu tiên kệ phù hợp nhất lên đầu: đúng loại bảo quản và còn đủ chỗ
        return result
            .OrderByDescending(x => x.IsSuitable)
            .ThenBy(x => x.StorageType == targetStorageType ? 0 : 1)
            .ThenByDescending(x => x.RemainingCapacityGrams)
            .ToList();
    }
    // [hieppm] End: Triển khai gợi ý kệ kho theo vị thuốc và khối lượng
}


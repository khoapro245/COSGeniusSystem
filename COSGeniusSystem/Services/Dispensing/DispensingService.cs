using COSGeniusSystem.Data;
using COSGeniusSystem.DTOs;
using COSGeniusSystem.Exceptions;
using COSGeniusSystem.Models;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace COSGeniusSystem.Services;

public class DispensingService : IDispensingService
{
    private readonly ApplicationDbContext _db;

    public DispensingService(ApplicationDbContext db)
    {
        _db = db;
    }

    public async Task<List<PendingPrescriptionDto>> GetPendingPrescriptionsAsync(string? keyword, CancellationToken ct = default)
    {
        var q = _db.HerbalPrescriptions
            .Include(p => p.Record)
                .ThenInclude(r => r.Patient)
            .Include(p => p.CreatedByNavigation)
            .Include(p => p.MedicineDispense)
            .Include(p => p.HerbalPrescriptionDetails)
            .Where(p => p.Status == "SENT" || p.Status == "PENDING")  // chỉ hiện đơn chưa bốc xong
            .AsNoTracking();

        if (!string.IsNullOrWhiteSpace(keyword))
        {
            var kw = keyword.Trim().ToLower();
            q = q.Where(p =>
                (p.PrescriptionCode != null && p.PrescriptionCode.ToLower().Contains(kw)) ||
                p.Record.Patient.FullName.ToLower().Contains(kw) ||
                (p.Record.Patient.PatientCode != null && p.Record.Patient.PatientCode.ToLower().Contains(kw)));
        }

        var prescriptions = await q
            .OrderByDescending(p => p.CreatedAt)
            .ToListAsync(ct);

        return prescriptions.Select(p =>
        {
            var md = p.MedicineDispense;
            string status = "NOT_IN_SLIP";
            string statusDisplay = "Chưa lập phiếu lĩnh";
            string? slipCode = null;

            if (md != null)
            {
                slipCode = md.DispenseCode ?? ("PL" + md.DispenseId.ToString("D5"));
                switch (md.Status)
                {
                    case "PENDING":
                        status = "WAITING_WAREHOUSE";
                        statusDisplay = "Chờ kho xử lý";
                        break;
                    case "WAREHOUSE_CONFIRMED":
                    case "ISSUED":
                        status = "WAITING_RECEIVE";
                        statusDisplay = "Kho xác nhận · Chờ nhận";
                        break;
                    case "RECEIVED":
                        status = "RECEIVED";
                        statusDisplay = "Đã nhận thuốc";
                        break;
                    case "COMPLETED":
                        status = "RECEIVED";
                        statusDisplay = "Kho đã xuất";
                        break;
                    case "REJECTED":
                        status = "REJECTED";
                        statusDisplay = "Kho từ chối";
                        break;
                    default:
                        status = "NOT_IN_SLIP";
                        statusDisplay = "Chưa lập phiếu lĩnh";
                        break;
                }
            }

            return new PendingPrescriptionDto
            {
                OrderId = p.PrescriptionId,
                OrderCode = p.PrescriptionCode ?? ("DT" + p.PrescriptionId.ToString("D5")),
                PatientName = p.Record.Patient.FullName,
                PatientCode = p.Record.Patient.PatientCode ?? ("BN" + p.Record.Patient.PatientId.ToString("D5")),
                DoctorName = p.CreatedByNavigation?.FullName ?? "Bác sĩ phụ trách",
                Doses = p.TotalDoses,
                Status = status,
                StatusDisplay = statusDisplay,
                LinkedSlipCode = slipCode,
                Items = p.HerbalPrescriptionDetails.Select(d => new OrderPrescriptionItemDto
                {
                    HerbName = d.HerbNameSnapshot,
                    DosagePerDose = d.DosagePerDose,
                    TotalDoseG = d.DosagePerDose * p.TotalDoses
                }).ToList()
            };
        }).ToList();
    }

    public async Task<AggregateSlipPreviewDto> PreviewAggregateSlipAsync(List<int> orderIds, CancellationToken ct = default)
    {
        if (orderIds == null || !orderIds.Any())
            throw new BadRequestException("Select at least one prescription to preview the matrix.");

        var orders = await _db.HerbalPrescriptions
            .Include(p => p.Record).ThenInclude(r => r.Patient)
            .Include(p => p.CreatedByNavigation)
            .Include(p => p.HerbalPrescriptionDetails)
            .Where(p => orderIds.Contains(p.PrescriptionId))
            .AsNoTracking()
            .ToListAsync(ct);

        var herbIds = orders.SelectMany(o => o.HerbalPrescriptionDetails.Select(d => d.HerbId)).Distinct().ToList();

        // Lấy tồn kho hiện tại theo từng HerbId từ bảng Batch
        var today = DateOnly.FromDateTime(DateTime.Today);
        var stocks = await _db.Batches
            .Where(b => herbIds.Contains(b.HerbId) && (b.Status == "ACTIVE" || b.Status == "Đạt chuẩn") && b.ExpiryDate >= today)
            .GroupBy(b => b.HerbId)
            .Select(g => new { HerbId = g.Key, StockQuantity = g.Sum(x => x.CurrentQuantity) })
            .ToDictionaryAsync(x => x.HerbId, x => x.StockQuantity, ct);

        var herbDict = new Dictionary<int, AggregateHerbCheckDto>();

        foreach (var ord in orders)
        {
            var ordCode = ord.PrescriptionCode ?? ("DT" + ord.PrescriptionId.ToString("D5"));
            foreach (var det in ord.HerbalPrescriptionDetails)
            {
                var grams = det.DosagePerDose * ord.TotalDoses;
                if (!herbDict.TryGetValue(det.HerbId, out var agg))
                {
                    var stock = stocks.GetValueOrDefault(det.HerbId, 0m);
                    agg = new AggregateHerbCheckDto
                    {
                        HerbId = det.HerbId,
                        HerbName = det.HerbNameSnapshot,
                        TotalG = 0,
                        StockKg = stock,
                        OrdersG = new Dictionary<string, decimal>()
                    };
                    herbDict[det.HerbId] = agg;
                }
                agg.TotalG += grams;
                agg.OrdersG[ordCode] = grams;
            }
        }

        var herbAggregates = herbDict.Values.Select(agg =>
        {
            agg.NeedKg = Math.Round(agg.TotalG / 1000m, 3);
            agg.IsEnough = agg.StockKg >= agg.NeedKg;
            agg.ShortageG = agg.IsEnough ? 0 : Math.Round((agg.NeedKg - agg.StockKg) * 1000m, 1);
            return agg;
        }).OrderBy(x => x.HerbName).ToList();

        return new AggregateSlipPreviewDto
        {
            Orders = orders.Select(p => new PendingPrescriptionDto
            {
                OrderId = p.PrescriptionId,
                OrderCode = p.PrescriptionCode ?? ("DT" + p.PrescriptionId.ToString("D5")),
                PatientName = p.Record.Patient.FullName,
                PatientCode = p.Record.Patient.PatientCode ?? ("BN" + p.Record.Patient.PatientId.ToString("D5")),
                DoctorName = p.CreatedByNavigation?.FullName ?? "Bác sĩ phụ trách",
                Doses = p.TotalDoses,
                Status = "NOT_IN_SLIP",
                StatusDisplay = "Đang chọn",
                Items = p.HerbalPrescriptionDetails.Select(d => new OrderPrescriptionItemDto
                {
                    HerbName = d.HerbNameSnapshot,
                    DosagePerDose = d.DosagePerDose,
                    TotalDoseG = d.DosagePerDose * p.TotalDoses
                }).ToList()
            }).ToList(),
            HerbAggregates = herbAggregates,
            AllHerbsInStock = herbAggregates.All(h => h.IsEnough)
        };
    }

    public async Task<DispensingSlipItemDto> CreateDispensingSlipAsync(CreateDispensingSlipRequest request, ClaimsPrincipal user, CancellationToken ct = default)
    {
        if (request.OrderIds == null || !request.OrderIds.Any())
            throw new BadRequestException("Select at least one prescription to create a requisition.");

        if (request.OrderIds.Count != 1)
            throw new ConflictException("Vui l?ng t?o y?u c?u c?p d??c li?u t?i m?n h?nh qu?y b?c thu?c.");
        var orderId = request.OrderIds[0];
        if (!await _db.HerbalPrescriptions.AnyAsync(p => p.PrescriptionId == orderId && p.Status == "SENT" && p.Record.Status == "COMPLETED" && p.Record.Registration.Status == "COMPLETED" && p.MedicineDispense == null
            && !_db.HerbRequisitionPrescriptions.Any(l => l.PrescriptionId == orderId && l.IsActive)
            && _db.InvoiceDetails.Any(i => i.PrescriptionId == orderId && i.Invoice.RecordId == p.RecordId && i.ItemType == "HERB_REMEDY" && i.IsActive && (i.Invoice.PaymentStatus == "PAID" || i.Invoice.PaymentStatus == "?? thanh to?n")), ct))
            throw new ConflictException("??n ch?a ?? ?i?u ki?n ho?c ch?a thanh to?n, ch?a th? t?o y?u c?u c?p d??c li?u.");
        var orders = await _db.HerbalPrescriptions
            .Include(p => p.HerbalPrescriptionDetails)
            .Where(p => request.OrderIds.Contains(p.PrescriptionId))
            .ToListAsync(ct);

        if (orders.Count == 0)
            throw new NotFoundException("None of the selected prescriptions were found.");

        var subClaim = user.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        int userId = int.TryParse(subClaim, out var uId) ? uId : orders.First().CreatedBy;

        var defaultWarehouse = await _db.Warehouses.Where(w => w.IsActive).OrderBy(w => w.WarehouseId).FirstOrDefaultAsync(ct)
            ?? throw new ConflictException("No available herbal warehouse was found in the system.");

        var firstOrder = orders.First();
        var slip = new MedicineDispense
        {
            PrescriptionId = firstOrder.PrescriptionId,
            WarehouseId = defaultWarehouse.WarehouseId,
            DispensedBy = userId,
            DispenseDate = DateTime.UtcNow,
            Status = "PENDING",
            Note = request.Note
        };

        _db.MedicineDispenses.Add(slip);
        await _db.SaveChangesAsync(ct);

        // Sinh mã phiếu nếu DB chưa tự sinh computed column
        if (string.IsNullOrEmpty(slip.DispenseCode))
        {
            slip.DispenseCode = "PL" + slip.DispenseId.ToString("D5");
            await _db.SaveChangesAsync(ct);
        }

        var creator = await _db.Users.FindAsync(new object[] { userId }, ct);
        decimal totalG = orders.Sum(o => o.HerbalPrescriptionDetails.Sum(d => d.DosagePerDose * o.TotalDoses));

        return new DispensingSlipItemDto
        {
            SlipId = slip.DispenseId,
            SlipCode = slip.DispenseCode,
            CreatedAt = slip.DispenseDate.ToString("dd/MM/yyyy HH:mm"),
            CreatedBy = creator?.FullName ?? "Herbalist",
            OrderCount = orders.Count,
            TotalQuantityG = totalG,
            Status = "PENDING"
        };
    }

    public async Task<List<DispensingSlipItemDto>> GetDispensingSlipsAsync(DispensingSlipFilterQuery query, CancellationToken ct = default)
    {
        var q = _db.MedicineDispenses
            .Include(d => d.DispensedByNavigation)
            .Include(d => d.Prescription).ThenInclude(p => p.HerbalPrescriptionDetails)
            .Include(d => d.Prescription).ThenInclude(p => p.Record).ThenInclude(r => r.Patient)
            .AsNoTracking();

        if (!string.IsNullOrWhiteSpace(query.Status))
        {
            var st = query.Status.Trim().ToUpperInvariant();
            // Chuẩn hoá alias: EXPORTED => WAREHOUSE_CONFIRMED
            if (st == "EXPORTED") st = "WAREHOUSE_CONFIRMED";
            q = q.Where(d =>
                d.Status.ToUpper() == st
                || (st == "WAREHOUSE_CONFIRMED" && (d.Status.ToUpper() == "ISSUED" || d.Status.ToUpper() == "COMPLETED"))
                || (st == "RECEIVED" && d.Status.ToUpper() == "COMPLETED"));
        }

        if (query.FromDate.HasValue)
            q = q.Where(d => d.DispenseDate >= query.FromDate.Value);

        if (query.ToDate.HasValue)
            q = q.Where(d => d.DispenseDate <= query.ToDate.Value);

        if (!string.IsNullOrWhiteSpace(query.Keyword))
        {
            var kw = query.Keyword.Trim().ToLower();
            q = q.Where(d =>
                (d.DispenseCode != null && d.DispenseCode.ToLower().Contains(kw)) ||
                (d.Prescription.PrescriptionCode != null && d.Prescription.PrescriptionCode.ToLower().Contains(kw)) ||
                d.Prescription.Record.Patient.FullName.ToLower().Contains(kw));
        }

        var page = query.Page < 1 ? 1 : query.Page;
        var pageSize = query.PageSize < 1 ? 20 : query.PageSize;

        return await q
            .OrderByDescending(d => d.DispenseDate)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(d => new DispensingSlipItemDto
            {
                SlipId = d.DispenseId,
                SlipCode = d.DispenseCode ?? ("PL" + d.DispenseId.ToString("D5")),
                CreatedAt = d.DispenseDate.ToString("dd/MM/yyyy HH:mm"),
                CreatedBy = d.DispensedByNavigation.FullName,
                OrderCount = 1,
                TotalQuantityG = d.Prescription.HerbalPrescriptionDetails.Sum(detail => detail.DosagePerDose * d.Prescription.TotalDoses),
                Status = (d.Status == "ISSUED" || d.Status == "COMPLETED") ? "WAREHOUSE_CONFIRMED" : d.Status,
                RejectReason = d.Status == "REJECTED" ? d.Note : null
            })
            .ToListAsync(ct);
    }

    public async Task<DispensingSlipDetailDto> GetDispensingSlipDetailAsync(int slipId, CancellationToken ct = default)
    {
        var d = await _db.MedicineDispenses
            .Include(x => x.DispensedByNavigation)
            .Include(x => x.DispenseDetails).ThenInclude(dd => dd.Herb)
            .Include(x => x.DispenseDetails).ThenInclude(dd => dd.Batch)
            .Include(x => x.Prescription).ThenInclude(p => p.HerbalPrescriptionDetails)
            .Include(x => x.Prescription).ThenInclude(p => p.CreatedByNavigation)
            .Include(x => x.Prescription).ThenInclude(p => p.Record).ThenInclude(r => r.Patient)
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.DispenseId == slipId, ct);

        if (d == null)
            throw new NotFoundException($"Herb requisition #{slipId} does not exist.");

        var p = d.Prescription;
        var herbs = d.DispenseDetails.Any()
            ? d.DispenseDetails.Select(dd => new HerbCheckItemDto
            {
                Id = dd.DispenseDetailId.ToString(),
                HerbId = dd.HerbId,
                HerbName = dd.Herb.CommonName,
                RequiredG = dd.QuantityTaken,
                ActualG = dd.QuantityTaken,
                BatchCode = dd.Batch?.BatchNumber ?? "LÔ-CHUNG",
                Verified = d.Status == "RECEIVED"
            }).ToList()
            : p.HerbalPrescriptionDetails.Select(det => new HerbCheckItemDto
            {
                Id = det.PrescriptionDetailId.ToString(),
                HerbId = det.HerbId,
                HerbName = det.HerbNameSnapshot,
                RequiredG = det.DosagePerDose * p.TotalDoses,
                ActualG = det.DosagePerDose * p.TotalDoses,
                BatchCode = "LÔ-" + det.HerbId,
                Verified = d.Status == "RECEIVED"
            }).ToList();

        var orderItems = p.HerbalPrescriptionDetails.Select(item => new OrderPrescriptionItemDto
        {
            HerbName = item.HerbNameSnapshot,
            DosagePerDose = item.DosagePerDose,
            TotalDoseG = item.DosagePerDose * p.TotalDoses
        }).ToList();

        var orders = new List<OrderInSlipDto>
        {
            new()
            {
                OrderId = p.PrescriptionId,
                OrderCode = p.PrescriptionCode ?? ("DT" + p.PrescriptionId.ToString("D5")),
                PatientName = p.Record.Patient.FullName,
                PatientCode = p.Record.Patient.PatientCode ?? ("BN" + p.Record.Patient.PatientId.ToString("D5")),
                DoctorName = p.CreatedByNavigation?.FullName ?? "Bác sĩ phụ trách",
                Doses = p.TotalDoses,
                Status = p.Status == "DISPENSED" ? "DISPENSED" : "PENDING",
                CompletedAt = p.Status == "DISPENSED" ? (p.UpdatedAt ?? d.DispenseDate).ToString("dd/MM/yyyy HH:mm") : null,
                Items = orderItems
            }
        };

        return new DispensingSlipDetailDto
        {
            SlipId = d.DispenseId,
            SlipCode = d.DispenseCode ?? ("PL" + d.DispenseId.ToString("D5")),
            CreatedBy = d.DispensedByNavigation.FullName,
            CreatedAt = d.DispenseDate.ToString("dd/MM/yyyy HH:mm"),
            OrderCount = orders.Count,
            Status = (d.Status == "ISSUED" || d.Status == "COMPLETED") ? "WAREHOUSE_CONFIRMED" : d.Status,
            WarehouseStaffInfo = $"NV kho: {d.DispensedByNavigation.FullName} · {d.DispenseDate:dd/MM/yyyy HH:mm}",
            Herbs = herbs,
            Orders = orders
        };
    }

    public async Task<bool> ConfirmReceivedAsync(int slipId, ClaimsPrincipal user, CancellationToken ct = default)
    {
        var slip = await _db.MedicineDispenses.FirstOrDefaultAsync(d => d.DispenseId == slipId, ct);
        if (slip == null)
            throw new NotFoundException($"Herb requisition #{slipId} does not exist.");

        if (slip.Status == "RECEIVED" || slip.Status == "COMPLETED")
            throw new ConflictException("Phiếu lĩnh đã được nhận hoặc hoàn tất trước đó.");

        if (slip.Status == "REJECTED")
            throw new ConflictException("Phiếu lĩnh đã bị từ chối, không thể xác nhận nhận.");

        // Chấp nhận cả WAREHOUSE_CONFIRMED lẫn ISSUED (tên cũ trong DB) và PENDING (bỏ qua bước kho nếu luồng rút gọn)
        slip.Status = "RECEIVED";
        await _db.SaveChangesAsync(ct);
        return true;
    }

    public async Task<bool> CompleteDispenseOrderAsync(int orderId, CompleteDispenseOrderRequest? request, ClaimsPrincipal user, CancellationToken ct = default)
    {
        var rx = await _db.HerbalPrescriptions
            .Include(p => p.HerbalPrescriptionDetails)
            .Include(p => p.MedicineDispense)
            .FirstOrDefaultAsync(p => p.PrescriptionId == orderId, ct);

        if (rx == null)
            throw new NotFoundException($"Prescription #{orderId} does not exist.");

        if (rx.Status == "DISPENSED")
            throw new ConflictException("Đơn thuốc này đã được bốc xong trước đó.");

        var subClaim = user.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        int userId = int.TryParse(subClaim, out var uId) ? uId : rx.CreatedBy;

        // ─── Trừ tồn kho lô thuốc theo lượng bốc thực tế ───
        var today = DateOnly.FromDateTime(DateTime.Today);
        int? warehouseId = rx.MedicineDispense?.WarehouseId;

        // Nếu kho đã xuất (WAREHOUSE_CONFIRMED/ISSUED/COMPLETED) thì DispenseDetail đã được ghi,
        // không cần trừ tồn lại — chỉ cập nhật trạng thái đơn.
        bool stockAlreadyDeducted = rx.MedicineDispense != null &&
            (rx.MedicineDispense.Status == "WAREHOUSE_CONFIRMED"
             || rx.MedicineDispense.Status == "ISSUED"
             || rx.MedicineDispense.Status == "COMPLETED"
             || await _db.DispenseDetails.AnyAsync(dd => dd.DispenseId == rx.MedicineDispense.DispenseId, ct));

        if (!stockAlreadyDeducted)
        {
            // Lấy danh sách phân bổ lô: ưu tiên request của frontend, fallback sang FEFO tự động
            List<(int BatchId, decimal QuantityGrams)> allocs;

            if (request?.BatchLines != null && request.BatchLines.Count > 0)
            {
                allocs = request.BatchLines
                    .Select(l => (l.BatchId, l.QuantityGrams))
                    .ToList();
            }
            else
            {
                // Tự động FEFO: lấy lô theo từng vị thuốc trong đơn
                var herbIds = rx.HerbalPrescriptionDetails.Select(d => d.HerbId).Distinct().ToList();
                var candidateBatches = await _db.Batches
                    .Where(b => herbIds.Contains(b.HerbId)
                        && (warehouseId == null || b.WarehouseId == warehouseId)
                        && (b.Status == "ACTIVE" || b.Status == "Đạt chuẩn")
                        && b.ExpiryDate >= today
                        && b.CurrentQuantity > 0)
                    .OrderBy(b => b.ExpiryDate)
                    .ThenBy(b => b.ImportDate)
                    .ThenBy(b => b.BatchId)
                    .ToListAsync(ct);

                allocs = new List<(int, decimal)>();
                foreach (var det in rx.HerbalPrescriptionDetails)
                {
                    var need = det.DosagePerDose * rx.TotalDoses;
                    foreach (var b in candidateBatches.Where(x => x.HerbId == det.HerbId))
                    {
                        if (need <= 0) break;
                        var avail = b.CurrentQuantity * 1000m;
                        if (avail <= 0) continue;
                        var take = Math.Min(need, avail);
                        allocs.Add((b.BatchId, take));
                        need -= take;
                    }
                }
            }

            if (allocs.Count > 0)
            {
                var batchIds = allocs.Select(a => a.BatchId).Distinct().ToList();
                var batches = await _db.Batches
                    .Where(b => batchIds.Contains(b.BatchId))
                    .ToDictionaryAsync(b => b.BatchId, ct);

                foreach (var (batchId, qtyGrams) in allocs)
                {
                    if (!batches.TryGetValue(batchId, out var batch))
                        throw new NotFoundException($"Lô hàng #{batchId} không tồn tại.");

                    var qtyKg = qtyGrams / 1000m;
                    if (batch.CurrentQuantity < qtyKg)
                        throw new ConflictException($"Lô {batch.BatchNumber} không đủ tồn (cần {qtyKg:F3} kg, còn {batch.CurrentQuantity:F3} kg).");

                    batch.CurrentQuantity -= qtyKg;

                    if (rx.MedicineDispense != null)
                    {
                        _db.DispenseDetails.Add(new DispenseDetail
                        {
                            DispenseId = rx.MedicineDispense.DispenseId,
                            BatchId = batchId,
                            HerbId = batch.HerbId,
                            QuantityTaken = qtyGrams
                        });
                    }
                }
            }
        }

        // Cập nhật trạng thái đơn & người bốc
        rx.Status = "DISPENSED";
        rx.UpdatedAt = DateTime.UtcNow;

        if (rx.MedicineDispense != null)
        {
            rx.MedicineDispense.DispensedBy = userId;
            // Chỉ cập nhật status phiếu lĩnh nếu nó chưa ở trạng thái hoàn thành
            var currentSlipStatus = rx.MedicineDispense.Status?.ToUpperInvariant();
            if (currentSlipStatus != "RECEIVED" && currentSlipStatus != "COMPLETED")
                rx.MedicineDispense.Status = "RECEIVED";
        }

        await _db.SaveChangesAsync(ct);
        return true;
    }

    public async Task<List<DispensedHistoryRecordDto>> GetDispenseHistoryAsync(DispenseHistoryFilterQuery query, CancellationToken ct = default)
    {
        var q = _db.HerbalPrescriptions
            .Include(p => p.Record).ThenInclude(r => r.Patient)
            .Include(p => p.CreatedByNavigation)
            .Include(p => p.MedicineDispense).ThenInclude(md => md.DispensedByNavigation)
            .Include(p => p.HerbalPrescriptionDetails)
            .Where(p => p.Status == "DISPENSED")
            .AsNoTracking();

        if (query.DispenserId.HasValue)
            q = q.Where(p => p.MedicineDispense != null && p.MedicineDispense.DispensedBy == query.DispenserId.Value);

        if (query.FromDate.HasValue)
            q = q.Where(p => (p.UpdatedAt ?? p.CreatedAt) >= query.FromDate.Value);

        if (query.ToDate.HasValue)
            q = q.Where(p => (p.UpdatedAt ?? p.CreatedAt) <= query.ToDate.Value);

        if (!string.IsNullOrWhiteSpace(query.Keyword))
        {
            var kw = query.Keyword.Trim().ToLower();
            q = q.Where(p =>
                (p.PrescriptionCode != null && p.PrescriptionCode.ToLower().Contains(kw)) ||
                p.Record.Patient.FullName.ToLower().Contains(kw) ||
                (p.Record.Patient.PatientCode != null && p.Record.Patient.PatientCode.ToLower().Contains(kw)));
        }

        var page = query.Page < 1 ? 1 : query.Page;
        var pageSize = query.PageSize < 1 ? 20 : query.PageSize;

        return await q
            .OrderByDescending(p => p.UpdatedAt ?? p.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(p => new DispensedHistoryRecordDto
            {
                OrderId = p.PrescriptionId,
                OrderCode = p.PrescriptionCode ?? ("DT" + p.PrescriptionId.ToString("D5")),
                PatientName = p.Record.Patient.FullName,
                PatientCode = p.Record.Patient.PatientCode ?? ("BN" + p.Record.Patient.PatientId.ToString("D5")),
                SlipCode = p.MedicineDispense != null ? (p.MedicineDispense.DispenseCode ?? ("PL" + p.MedicineDispense.DispenseId.ToString("D5"))) : "PL00000",
                Doses = p.TotalDoses,
                Dispenser = p.MedicineDispense != null && p.MedicineDispense.DispensedByNavigation != null ? p.MedicineDispense.DispensedByNavigation.FullName : "Người bốc thuốc",
                CompletedAt = (p.UpdatedAt ?? p.CreatedAt).ToString("dd/MM/yyyy HH:mm"),
                DoctorName = p.CreatedByNavigation != null ? p.CreatedByNavigation.FullName : "Bác sĩ",
                Items = p.HerbalPrescriptionDetails.Select(d => new OrderPrescriptionItemDto
                {
                    HerbName = d.HerbNameSnapshot,
                    DosagePerDose = d.DosagePerDose,
                    TotalDoseG = d.DosagePerDose * p.TotalDoses
                }).ToList()
            })
            .ToListAsync(ct);
    }

    // ─── TRIỂN KHAI NGHIỆP VỤ XUẤT KHO (WH-11 -> WH-14) ───

    public async Task<List<FefoHerbSuggestionDto>> GetFefoSuggestionsAsync(int slipId, CancellationToken ct = default)
    {
        var slip = await _db.MedicineDispenses
            .Include(s => s.Prescription)
                .ThenInclude(p => p.HerbalPrescriptionDetails)
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.DispenseId == slipId, ct);

        if (slip == null)
            throw new NotFoundException($"Phiếu lĩnh thuốc #{slipId} không tồn tại.");

        var details = slip.Prescription.HerbalPrescriptionDetails;
        var herbIds = details.Select(d => d.HerbId).Distinct().ToList();
        var today = DateOnly.FromDateTime(DateTime.Today);

        var batches = await _db.Batches
            .Include(b => b.StorageLocation)
            .Where(b => herbIds.Contains(b.HerbId) && b.WarehouseId == slip.WarehouseId && (b.Status == "ACTIVE" || b.Status == "Đạt chuẩn") && b.ExpiryDate >= today && b.CurrentQuantity > 0)
            .OrderBy(b => b.ExpiryDate)
            .ThenBy(b => b.ImportDate)
            .ThenBy(b => b.BatchId)
            .AsNoTracking()
            .ToListAsync(ct);

        var results = new List<FefoHerbSuggestionDto>();

        foreach (var det in details)
        {
            var reqGrams = det.DosagePerDose * slip.Prescription.TotalDoses;
            var remainingGrams = reqGrams;
            var lines = new List<FefoSuggestedLineDto>();

            var candidateBatches = batches.Where(b => b.HerbId == det.HerbId);

            foreach (var b in candidateBatches)
            {
                var availGrams = b.CurrentQuantity * 1000m; // quy đổi kg sang g
                if (availGrams <= 0) continue;

                var allocGrams = Math.Min(remainingGrams, availGrams);
                lines.Add(new FefoSuggestedLineDto
                {
                    BatchId = b.BatchId,
                    BatchNumber = b.BatchNumber,
                    ExpiryDate = b.ExpiryDate.ToString("dd/MM/yyyy"),
                    Location = b.StorageLocation?.Code ?? "Kệ chung",
                    QuantityGrams = allocGrams,
                    AvailableGrams = availGrams
                });

                remainingGrams -= allocGrams;
                if (remainingGrams <= 0) break;
            }

            results.Add(new FefoHerbSuggestionDto
            {
                HerbId = det.HerbId,
                HerbName = det.HerbNameSnapshot,
                RequiredGrams = reqGrams,
                ShortageGrams = Math.Max(0, remainingGrams),
                Lines = lines
            });
        }

        return results;
    }

    public async Task<bool> ApproveAndIssueWarehouseAsync(int slipId, WarehouseIssueRequest request, ClaimsPrincipal user, CancellationToken ct = default)
    {
        var slip = await _db.MedicineDispenses
            .Include(s => s.DispenseDetails)
            .Include(s => s.Prescription)
                .ThenInclude(p => p.HerbalPrescriptionDetails)
            .FirstOrDefaultAsync(s => s.DispenseId == slipId, ct);

        if (slip == null)
            throw new NotFoundException($"Phiếu lĩnh thuốc #{slipId} không tồn tại.");

        if (slip.Status == "WAREHOUSE_CONFIRMED" || slip.Status == "ISSUED" || slip.Status == "RECEIVED" || slip.Status == "COMPLETED")
            throw new ConflictException("Phiếu lĩnh này đã được xuất kho trước đó.");

        var subClaim = user.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        int warehouseUserId = int.TryParse(subClaim, out var uId) ? uId : slip.DispensedBy;

        var details = slip.Prescription.HerbalPrescriptionDetails;
        var today = DateOnly.FromDateTime(DateTime.Today);

        // Nếu request gửi lên danh sách phân bổ lô cụ thể, sử dụng danh sách đó; ngược lại tự động tính FEFO
        var allocations = request?.Allocations != null && request.Allocations.Any()
            ? request.Allocations
            : new List<BatchAllocationItem>();

        if (!allocations.Any())
        {
            var suggestions = await GetFefoSuggestionsAsync(slipId, ct);
            if (suggestions.Any(s => s.ShortageGrams > 0))
            {
                throw new ConflictException("Kho không đủ số lượng tồn của một số vị thuốc để xuất kho.");
            }
            allocations = suggestions.SelectMany(s => s.Lines.Select(l => new BatchAllocationItem
            {
                BatchId = l.BatchId,
                QuantityGrams = l.QuantityGrams
            })).ToList();
        }

        var batchIds = allocations.Select(a => a.BatchId).Distinct().ToList();
        var batches = await _db.Batches
            .Where(b => batchIds.Contains(b.BatchId))
            .ToDictionaryAsync(b => b.BatchId, ct);

        // Trừ tồn kho và ghi nhận DispenseDetail
        foreach (var item in allocations)
        {
            if (!batches.TryGetValue(item.BatchId, out var batch))
                throw new NotFoundException($"Lô hàng #{item.BatchId} không tồn tại.");

            var qtyKg = item.QuantityGrams / 1000m;
            if (batch.CurrentQuantity < qtyKg)
                throw new ConflictException($"Lô hàng {batch.BatchNumber} không đủ số lượng tồn (Yêu cầu: {qtyKg}kg, Hiện có: {batch.CurrentQuantity}kg).");

            batch.CurrentQuantity -= qtyKg;

            var dispenseDetail = new DispenseDetail
            {
                DispenseId = slip.DispenseId,
                BatchId = batch.BatchId,
                HerbId = batch.HerbId,
                QuantityTaken = item.QuantityGrams
            };
            _db.DispenseDetails.Add(dispenseDetail);
        }

        slip.Status = "WAREHOUSE_CONFIRMED";
        slip.DispenseDate = DateTime.UtcNow;

        await _db.SaveChangesAsync(ct);
        return true;
    }

    public async Task<bool> RejectWarehouseSlipAsync(int slipId, WarehouseRejectRequest request, ClaimsPrincipal user, CancellationToken ct = default)
    {
        var slip = await _db.MedicineDispenses.FirstOrDefaultAsync(s => s.DispenseId == slipId, ct);
        if (slip == null)
            throw new NotFoundException($"Phiếu lĩnh thuốc #{slipId} không tồn tại.");

        if (slip.Status == "RECEIVED")
            throw new ConflictException("Phiếu lĩnh đã được nhận thuốc, không thể từ chối.");

        if (slip.Status == "COMPLETED" || slip.Status == "WAREHOUSE_CONFIRMED" || slip.Status == "ISSUED")
            throw new ConflictException("Phiếu lĩnh đã được kho xác nhận xuất, không thể từ chối.");

        slip.Status = "REJECTED";
        slip.Note = string.IsNullOrWhiteSpace(request?.Reason)
            ? "Kho từ chối xuất do không đáp ứng đủ điều kiện tồn kho."
            : request.Reason.Trim();

        await _db.SaveChangesAsync(ct);
        return true;
    }
}

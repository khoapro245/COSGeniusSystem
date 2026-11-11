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
            .Where(p => p.Status == "SENT")
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
                        status = "WAITING_RECEIVE";
                        statusDisplay = "Kho xác nhận · Chờ nhận";
                        break;
                    case "RECEIVED":
                        status = "RECEIVED";
                        statusDisplay = "Đã nhận thuốc";
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
            throw new BadRequestException("Vui lòng chọn ít nhất 1 đơn thuốc để xem ma trận.");

        var orders = await _db.HerbalPrescriptions
            .Include(p => p.Record).ThenInclude(r => r.Patient)
            .Include(p => p.CreatedByNavigation)
            .Include(p => p.HerbalPrescriptionDetails)
            .Where(p => orderIds.Contains(p.PrescriptionId))
            .AsNoTracking()
            .ToListAsync(ct);

        var herbIds = orders.SelectMany(o => o.HerbalPrescriptionDetails.Select(d => d.HerbId)).Distinct().ToList();

        // Lấy tồn kho hiện tại theo từng HerbId từ bảng Batch
        var stocks = await _db.Batches
            .Where(b => herbIds.Contains(b.HerbId) && b.Status == "ACTIVE" && b.ExpiryDate >= DateOnly.FromDateTime(DateTime.Today))
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
            throw new BadRequestException("Vui lòng chọn ít nhất 1 đơn thuốc để tạo phiếu.");

        var orders = await _db.HerbalPrescriptions
            .Include(p => p.HerbalPrescriptionDetails)
            .Where(p => request.OrderIds.Contains(p.PrescriptionId))
            .ToListAsync(ct);

        if (orders.Count == 0)
            throw new NotFoundException("Không tìm thấy đơn thuốc nào được chọn.");

        var subClaim = user.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        int userId = int.TryParse(subClaim, out var uId) ? uId : orders.First().CreatedBy;

        var defaultWarehouse = await _db.Warehouses.Where(w => w.IsActive).OrderBy(w => w.WarehouseId).FirstOrDefaultAsync(ct)
            ?? throw new ConflictException("Không tìm thấy kho dược liệu khả dụng trong hệ thống.");

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
            q = q.Where(d => d.Status.ToUpper() == st);
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
                Status = d.Status,
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
            throw new NotFoundException($"Phiếu lĩnh thuốc #{slipId} không tồn tại.");

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
            Status = d.Status,
            WarehouseStaffInfo = $"NV kho: {d.DispensedByNavigation.FullName} · {d.DispenseDate:dd/MM/yyyy HH:mm}",
            Herbs = herbs,
            Orders = orders
        };
    }

    public async Task<bool> ConfirmReceivedAsync(int slipId, ClaimsPrincipal user, CancellationToken ct = default)
    {
        var slip = await _db.MedicineDispenses.FirstOrDefaultAsync(d => d.DispenseId == slipId, ct);
        if (slip == null)
            throw new NotFoundException($"Phiếu lĩnh thuốc #{slipId} không tồn tại.");

        slip.Status = "RECEIVED";
        await _db.SaveChangesAsync(ct);
        return true;
    }

    public async Task<bool> CompleteDispenseOrderAsync(int orderId, ClaimsPrincipal user, CancellationToken ct = default)
    {
        var rx = await _db.HerbalPrescriptions.FirstOrDefaultAsync(p => p.PrescriptionId == orderId, ct);
        if (rx == null)
            throw new NotFoundException($"Đơn thuốc #{orderId} không tồn tại.");

        rx.Status = "DISPENSED";
        rx.UpdatedAt = DateTime.UtcNow;

        var subClaim = user.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (int.TryParse(subClaim, out var userId))
        {
            var slip = await _db.MedicineDispenses.FirstOrDefaultAsync(d => d.PrescriptionId == orderId, ct);
            if (slip != null) slip.DispensedBy = userId;
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
}

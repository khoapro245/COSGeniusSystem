using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using COSGeniusSystem.Commons;
using COSGeniusSystem.Data;
using COSGeniusSystem.DTOs;
using COSGeniusSystem.Models;
using Microsoft.EntityFrameworkCore;

namespace COSGeniusSystem.Services;

public class ReportService : IReportService
{
    private readonly ApplicationDbContext _context;

    public ReportService(ApplicationDbContext context)
    {
        _context = context;
    }

    /// <summary>
    /// Báo cáo nhập kho:
    /// - Nhập bao nhiêu đơn rồi (TotalOrders)
    /// - Nhập các vị thuốc nào của nhà cung cấp nào (BySupplier, ByHerb, Receipts.Details)
    /// - Tốn bao nhiêu tiền (TotalSpent)
    /// </summary>
    public async Task<StockImportReportDto> GetStockImportReportAsync(ReportFilterRequest filter, CancellationToken ct = default)
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
            .Include(g => g.ReceiptDetails)
                .ThenInclude(d => d.Batch)
                    .ThenInclude(b => b.StorageLocation)
            .AsQueryable();

        // 1. Lọc theo khoảng ngày (Từ ngày - Đến ngày)
        if (filter.FromDate.HasValue)
        {
            var start = filter.FromDate.Value.Date;
            query = query.Where(g => g.ReceiptDate >= start);
        }

        if (filter.ToDate.HasValue)
        {
            var end = filter.ToDate.Value.Date.AddDays(1);
            query = query.Where(g => g.ReceiptDate < end);
        }

        // 2. Lọc theo nhà cung cấp
        if (filter.SupplierId.HasValue && filter.SupplierId.Value > 0)
        {
            query = query.Where(g => g.Po != null && g.Po.SupplierId == filter.SupplierId.Value);
        }

        // 3. Lọc theo từ khóa (Mã phiếu, PO, Tên NCC, Ghi chú)
        if (!string.IsNullOrWhiteSpace(filter.Keyword))
        {
            var kw = filter.Keyword.Trim().ToLower();
            query = query.Where(g =>
                g.ReceiptCode.ToLower().Contains(kw) ||
                (g.Po != null && g.Po.Pocode.ToLower().Contains(kw)) ||
                (g.Po != null && g.Po.Supplier.SupplierName.ToLower().Contains(kw)) ||
                (g.Note != null && g.Note.ToLower().Contains(kw))
            );
        }

        var receipts = await query
            .OrderByDescending(g => g.ReceiptDate)
            .ThenByDescending(g => g.ReceiptId)
            .ToListAsync(ct);

        var reportDto = new StockImportReportDto
        {
            FromDate = filter.FromDate,
            ToDate = filter.ToDate,
            TotalOrders = receipts.Count,
            TotalSpent = receipts.Sum(r => r.TotalCost)
        };

        var supplierMap = new Dictionary<int, SupplierImportSummaryDto>();
        var herbMap = new Dictionary<int, HerbImportSummaryDto>();
        decimal overallTotalQty = 0;

        foreach (var r in receipts)
        {
            var supplierId = r.Po?.SupplierId ?? 0;
            var supplierName = r.Po?.Supplier?.SupplierName ?? (!string.IsNullOrEmpty(r.Note) && r.Note.Contains("demo") ? "Kho tổng Nội bộ (Tồn đầu kỳ)" : "Nhà cung cấp đối tác");
            var supplierCode = r.Po?.Supplier?.SupplierCode ?? (supplierId > 0 ? $"NCC-{supplierId}" : "NCC-INTERNAL");

            if (!supplierMap.TryGetValue(supplierId, out var supSummary))
            {
                supSummary = new SupplierImportSummaryDto
                {
                    SupplierId = supplierId,
                    SupplierCode = supplierCode,
                    SupplierName = supplierName,
                    OrderCount = 0,
                    TotalSpent = 0,
                    TotalQuantityGrams = 0,
                    SuppliedHerbNames = new List<string>()
                };
                supplierMap[supplierId] = supSummary;
            }

            supSummary.OrderCount++;
            supSummary.TotalSpent += r.TotalCost;

            var receiptDto = new ImportReceiptItemDto
            {
                ReceiptId = r.ReceiptId,
                ReceiptCode = r.ReceiptCode,
                ReceiptDate = r.ReceiptDate,
                PoCode = r.Po?.Pocode,
                SupplierName = supplierName,
                ReceiverName = r.ReceivedByNavigation?.FullName ?? "Thủ kho",
                WarehouseName = r.Warehouse?.WarehouseName ?? "Kho chính",
                Status = r.Status,
                StatusLabel = r.Status switch
                {
                    GoodsReceiptStatusConstants.Approved or "Đã duyệt" => "Đã duyệt",
                    GoodsReceiptStatusConstants.Rejected or "Từ chối" => "Từ chối",
                    _ => "Chờ duyệt"
                },
                TotalCost = r.TotalCost,
                Note = r.Note
            };

            decimal receiptQty = 0;

            foreach (var d in r.ReceiptDetails)
            {
                var herb = d.Batch?.Herb;
                var herbId = herb?.HerbId ?? d.Batch?.HerbId ?? 0;
                var herbCode = herb?.HerbCode ?? $"HERB-{herbId}";
                var herbName = herb?.CommonName ?? "Dược liệu";
                var unit = herb?.Unit ?? "g";
                var qty = d.Quantity;
                var unitPrice = d.UnitPrice;
                var lineTotal = d.LineTotal ?? (qty * unitPrice);

                receiptQty += qty;
                overallTotalQty += qty;
                supSummary.TotalQuantityGrams += qty;

                if (!supSummary.SuppliedHerbNames.Contains(herbName))
                {
                    supSummary.SuppliedHerbNames.Add(herbName);
                }

                // Gom nhóm theo vị thuốc
                if (!herbMap.TryGetValue(herbId, out var herbSummary))
                {
                    herbSummary = new HerbImportSummaryDto
                    {
                        HerbId = herbId,
                        HerbCode = herbCode,
                        HerbName = herbName,
                        Unit = unit,
                        TotalQuantity = 0,
                        TotalSpent = 0,
                        Suppliers = new List<string>()
                    };
                    herbMap[herbId] = herbSummary;
                }

                herbSummary.TotalQuantity += qty;
                herbSummary.TotalSpent += lineTotal;
                if (!herbSummary.Suppliers.Contains(supplierName))
                {
                    herbSummary.Suppliers.Add(supplierName);
                }

                receiptDto.Details.Add(new ImportReceiptDetailItemDto
                {
                    HerbId = herbId,
                    HerbCode = herbCode,
                    HerbName = herbName,
                    BatchNumber = d.Batch?.BatchNumber ?? "",
                    Quantity = qty,
                    Unit = unit,
                    UnitPrice = unitPrice,
                    LineTotal = lineTotal,
                    ExpiryDate = d.Batch != null ? d.Batch.ExpiryDate.ToString("yyyy-MM-dd") : "",
                    Location = d.Batch?.StorageLocation?.Code ?? d.Batch?.Location ?? "Kho chính"
                });
            }

            receiptDto.TotalQuantity = receiptQty;
            reportDto.Receipts.Add(receiptDto);
        }

        reportDto.TotalQuantityGrams = overallTotalQty;
        reportDto.TotalSuppliers = supplierMap.Count;

        // Tính tỷ trọng chi tiêu từng NCC
        foreach (var s in supplierMap.Values)
        {
            s.PercentOfTotalSpent = reportDto.TotalSpent > 0
                ? decimal.Round((s.TotalSpent / reportDto.TotalSpent) * 100m, 1, MidpointRounding.AwayFromZero)
                : 0m;
        }

        // Tính đơn giá bình quân từng Vị thuốc
        foreach (var h in herbMap.Values)
        {
            h.AverageUnitPrice = h.TotalQuantity > 0
                ? decimal.Round(h.TotalSpent / h.TotalQuantity, 2, MidpointRounding.AwayFromZero)
                : 0m;
        }

        reportDto.BySupplier = supplierMap.Values.OrderByDescending(s => s.TotalSpent).ToList();
        reportDto.ByHerb = herbMap.Values.OrderByDescending(h => h.TotalSpent).ToList();

        return reportDto;
    }

    /// <summary>
    /// Báo cáo xuất kho:
    /// - Bao nhiêu đơn thuốc được bán ra (TotalPrescriptionsSold)
    /// - Bao nhiêu đơn vị thuốc được bán ra (TotalHerbUnitsSold)
    /// - Thu được bao nhiêu tiền (TotalRevenue)
    /// - Lãi thực tế được bao nhiêu tiền (ActualProfit = TotalRevenue - TotalCost)
    /// </summary>
    public async Task<StockExportReportDto> GetStockExportReportAsync(ReportFilterRequest filter, CancellationToken ct = default)
    {
        // 1. Tải trước từ điển chi phí giá vốn của từng Herb từ Batch gần nhất để tính chuẩn xác giá vốn
        var latestBatchCosts = await _context.Batches
            .AsNoTracking()
            .Where(b => b.UnitCost > 0)
            .OrderByDescending(b => b.ImportDate)
            .GroupBy(b => b.HerbId)
            .Select(g => new { HerbId = g.Key, UnitCost = g.First().UnitCost })
            .ToDictionaryAsync(x => x.HerbId, x => x.UnitCost, ct);

        // 2. Tải danh sách đơn thuốc (kèm chi tiết các vị thuốc, phiếu xuất kho, hóa đơn liên kết)
        var rxQuery = _context.HerbalPrescriptions
            .AsNoTracking()
            .Include(p => p.CreatedByNavigation)
            .Include(p => p.Record)
                .ThenInclude(r => r.Patient)
            .Include(p => p.Record)
                .ThenInclude(r => r.Doctor)
            .Include(p => p.HerbalPrescriptionDetails)
                .ThenInclude(d => d.Herb)
            .Include(p => p.MedicineDispense)
                .ThenInclude(m => m!.DispenseDetails)
                    .ThenInclude(dd => dd.Batch)
            .Include(p => p.InvoiceDetails)
                .ThenInclude(id => id.Invoice)
                    .ThenInclude(inv => inv.Cashier)
            .AsQueryable();

        var allRx = await rxQuery
            .OrderByDescending(p => p.CreatedAt)
            .ThenByDescending(p => p.PrescriptionId)
            .ToListAsync(ct);

        var reportDto = new StockExportReportDto
        {
            FromDate = filter.FromDate,
            ToDate = filter.ToDate
        };

        var herbExportMap = new Dictionary<int, HerbExportSummaryDto>();
        var patientExportMap = new Dictionary<string, PatientExportSummaryDto>();
        var dailyTrendMap = new Dictionary<string, DailyExportTrendDto>();

        foreach (var rx in allRx)
        {
            // Xác định ngày giao dịch xuất bán
            var invoiceDetail = rx.InvoiceDetails.FirstOrDefault(i => i.ItemType == "HERB_REMEDY");
            var invoice = invoiceDetail?.Invoice;

            DateTime txDate = invoice?.PaidAt ?? invoice?.IssuedDate ?? rx.MedicineDispense?.DispenseDate ?? rx.CreatedAt;

            // Kiểm tra bộ lọc ngày
            if (filter.FromDate.HasValue && txDate.Date < filter.FromDate.Value.Date)
            {
                continue;
            }
            if (filter.ToDate.HasValue && txDate.Date > filter.ToDate.Value.Date)
            {
                continue;
            }

            // Kiểm tra từ khóa
            if (!string.IsNullOrWhiteSpace(filter.Keyword))
            {
                var kw = filter.Keyword.Trim().ToLower();
                var patientName = rx.Record?.Patient?.FullName?.ToLower() ?? "";
                var patientCode = rx.Record?.Patient?.PatientCode?.ToLower() ?? "";
                var docName = rx.Record?.Doctor?.FullName?.ToLower() ?? "";
                var rxCode = rx.PrescriptionCode?.ToLower() ?? "";
                var rxName = rx.PrescriptionName?.ToLower() ?? "";
                var invCode = invoice?.InvoiceCode?.ToLower() ?? "";

                bool match = patientName.Contains(kw) || patientCode.Contains(kw) || docName.Contains(kw) ||
                             rxCode.Contains(kw) || rxName.Contains(kw) || invCode.Contains(kw);

                if (!match) continue;
            }

            int totalDoses = rx.TotalDoses > 0 ? rx.TotalDoses : 1;
            decimal rxRevenue = 0;
            decimal rxCost = 0;

            var saleItem = new PrescriptionSaleItemDto
            {
                PrescriptionId = rx.PrescriptionId,
                PrescriptionCode = rx.PrescriptionCode ?? $"RX-{rx.PrescriptionId:D6}",
                PrescriptionName = rx.PrescriptionName ?? "Bài thuốc điều trị",
                PatientName = rx.Record?.Patient?.FullName ?? invoice?.PatientNameSnapshot ?? "Bệnh nhân",
                PatientCode = rx.Record?.Patient?.PatientCode ?? invoice?.PatientCodeSnapshot ?? "BN",
                DoctorName = rx.Record?.Doctor?.FullName ?? invoice?.DoctorNameSnapshot ?? rx.CreatedByNavigation?.FullName ?? "Bác sĩ",
                SoldDate = txDate,
                TotalDoses = totalDoses,
                Status = rx.Status,
                StatusLabel = rx.Status switch
                {
                    "DISPENSED" or "COMPLETED" => "Đã xuất kho",
                    "PAID" => "Đã thanh toán",
                    "APPROVED" => "Đã duyệt bốc thuốc",
                    "SENT" => "Đã gửi phòng dược",
                    _ => rx.Status
                },
                InvoiceCode = invoice?.InvoiceCode,
                PaymentMethod = invoice?.PaymentMethod ?? "Tiền mặt"
            };

            // Duyệt từng vị thuốc trong đơn thuốc
            foreach (var detail in rx.HerbalPrescriptionDetails)
            {
                var herb = detail.Herb;
                var herbId = detail.HerbId;
                var herbCode = herb?.HerbCode ?? $"V{herbId}";
                var herbName = detail.HerbNameSnapshot ?? herb?.CommonName ?? "Vị thuốc";
                var unit = herb?.Unit ?? "g";

                // Đơn giá bán chuẩn per gram
                decimal sellingPricePerGram = herb != null
                    ? (herb.Unit == "kg" ? herb.StandardPrice / 1000m : herb.StandardPrice)
                    : 250m;

                // Giá vốn per gram: lấy từ lô thuốc hoặc từ bảng Batch
                decimal costPricePerGram = 0m;
                var dispenseDetail = rx.MedicineDispense?.DispenseDetails?.FirstOrDefault(dd => dd.HerbId == herbId);
                if (dispenseDetail?.Batch != null && dispenseDetail.Batch.UnitCost > 0)
                {
                    costPricePerGram = dispenseDetail.Batch.UnitCost;
                }
                else if (latestBatchCosts.TryGetValue(herbId, out var bCost))
                {
                    costPricePerGram = bCost;
                }
                else if (herb?.LastImportPrice.HasValue == true && herb.LastImportPrice.Value > 0)
                {
                    costPricePerGram = herb.Unit == "kg" ? herb.LastImportPrice.Value / 1000m : herb.LastImportPrice.Value;
                }
                else
                {
                    costPricePerGram = decimal.Round(sellingPricePerGram * 0.6m, 2); // Ước tính biên an toàn 40%
                }

                decimal dosagePerDose = detail.DosagePerDose > 0 ? detail.DosagePerDose : 10m;
                decimal totalLineQty = dosagePerDose * totalDoses;

                decimal lineRevenue = decimal.Round(totalLineQty * sellingPricePerGram, 0, MidpointRounding.AwayFromZero);
                decimal lineCost = decimal.Round(totalLineQty * costPricePerGram, 0, MidpointRounding.AwayFromZero);
                decimal lineProfit = lineRevenue - lineCost;
                decimal lineMargin = lineRevenue > 0 ? decimal.Round((lineProfit / lineRevenue) * 100m, 1, MidpointRounding.AwayFromZero) : 0m;

                rxRevenue += lineRevenue;
                rxCost += lineCost;

                saleItem.Details.Add(new PrescriptionSaleDetailItemDto
                {
                    HerbId = herbId,
                    HerbCode = herbCode,
                    HerbName = herbName,
                    DosagePerDose = dosagePerDose,
                    TotalQuantity = totalLineQty,
                    Unit = unit,
                    SellingPrice = sellingPricePerGram,
                    CostPrice = costPricePerGram,
                    SubTotalRevenue = lineRevenue,
                    SubTotalCost = lineCost,
                    LineProfit = lineProfit,
                    ProfitMargin = lineMargin
                });

                // Gom nhóm theo Vị thuốc
                if (!herbExportMap.TryGetValue(herbId, out var herbSummary))
                {
                    herbSummary = new HerbExportSummaryDto
                    {
                        HerbId = herbId,
                        HerbCode = herbCode,
                        HerbName = herbName,
                        Unit = unit,
                        TotalQuantitySold = 0,
                        TotalRevenue = 0,
                        TotalCost = 0,
                        ActualProfit = 0,
                        PrescriptionCount = 0
                    };
                    herbExportMap[herbId] = herbSummary;
                }

                herbSummary.TotalQuantitySold += totalLineQty;
                herbSummary.TotalRevenue += lineRevenue;
                herbSummary.TotalCost += lineCost;
                herbSummary.ActualProfit += lineProfit;
                herbSummary.PrescriptionCount++;
            }

            // Nếu đơn thuốc có ghi nhận trên InvoiceDetail chính xác thì ưu tiên doanh thu thực thu
            if (invoiceDetail != null && invoiceDetail.SubTotal.HasValue && invoiceDetail.SubTotal.Value > 0)
            {
                rxRevenue = invoiceDetail.SubTotal.Value;
            }

            decimal rxProfit = rxRevenue - rxCost;
            saleItem.Revenue = rxRevenue;
            saleItem.Cost = rxCost;
            saleItem.Profit = rxProfit;
            saleItem.ProfitMargin = rxRevenue > 0 ? decimal.Round((rxProfit / rxRevenue) * 100m, 1, MidpointRounding.AwayFromZero) : 0m;

            reportDto.Prescriptions.Add(saleItem);

            // Gom nhóm theo Bệnh nhân
            var patientKey = !string.IsNullOrWhiteSpace(saleItem.PatientCode) ? saleItem.PatientCode : saleItem.PatientName;
            if (!patientExportMap.TryGetValue(patientKey, out var patientSummary))
            {
                patientSummary = new PatientExportSummaryDto
                {
                    PatientCode = saleItem.PatientCode,
                    PatientName = saleItem.PatientName,
                    PrescriptionCount = 0,
                    TotalDoses = 0,
                    TotalRevenue = 0,
                    TotalCost = 0,
                    TotalProfit = 0
                };
                patientExportMap[patientKey] = patientSummary;
            }

            patientSummary.PrescriptionCount++;
            patientSummary.TotalDoses += totalDoses;
            patientSummary.TotalRevenue += rxRevenue;
            patientSummary.TotalCost += rxCost;
            patientSummary.TotalProfit += rxProfit;

            // Gom nhóm xu hướng theo ngày
            var dateKey = txDate.ToString("yyyy-MM-dd");
            if (!dailyTrendMap.TryGetValue(dateKey, out var dailyDto))
            {
                dailyDto = new DailyExportTrendDto
                {
                    Date = dateKey,
                    PrescriptionsCount = 0,
                    Revenue = 0,
                    Cost = 0,
                    Profit = 0
                };
                dailyTrendMap[dateKey] = dailyDto;
            }

            dailyDto.PrescriptionsCount++;
            dailyDto.Revenue += rxRevenue;
            dailyDto.Cost += rxCost;
            dailyDto.Profit += rxProfit;
        }

        // Tổng kết toàn kỳ
        reportDto.TotalPatients = patientExportMap.Count;
        reportDto.TotalPrescriptionsSold = reportDto.Prescriptions.Count;
        reportDto.TotalDosesSold = reportDto.Prescriptions.Sum(p => p.TotalDoses);
        reportDto.TotalRevenue = reportDto.Prescriptions.Sum(p => p.Revenue);
        reportDto.TotalCost = reportDto.Prescriptions.Sum(p => p.Cost);
        reportDto.ActualProfit = reportDto.TotalRevenue - reportDto.TotalCost;
        reportDto.ProfitMargin = reportDto.TotalRevenue > 0
            ? decimal.Round((reportDto.ActualProfit / reportDto.TotalRevenue) * 100m, 1, MidpointRounding.AwayFromZero)
            : 0m;

        // Tính tổng lượng đơn vị thuốc bán ra và biên lợi nhuận từng vị thuốc
        foreach (var h in herbExportMap.Values)
        {
            h.ProfitMargin = h.TotalRevenue > 0
                ? decimal.Round((h.ActualProfit / h.TotalRevenue) * 100m, 1, MidpointRounding.AwayFromZero)
                : 0m;
        }

        // Tính biên lợi nhuận từng bệnh nhân
        foreach (var p in patientExportMap.Values)
        {
            p.ProfitMargin = p.TotalRevenue > 0
                ? decimal.Round((p.TotalProfit / p.TotalRevenue) * 100m, 1, MidpointRounding.AwayFromZero)
                : 0m;
        }

        reportDto.TotalHerbUnitsSold = herbExportMap.Values.Sum(h => h.TotalQuantitySold);
        reportDto.ByHerb = herbExportMap.Values.OrderByDescending(h => h.TotalRevenue).ToList();
        reportDto.ByPatient = patientExportMap.Values.OrderByDescending(p => p.TotalRevenue).ToList();
        reportDto.DailyTrends = dailyTrendMap.Values.OrderBy(d => d.Date).ToList();

        return reportDto;
    }
}


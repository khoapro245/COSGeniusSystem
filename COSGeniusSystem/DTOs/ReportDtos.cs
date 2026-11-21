using System;
using System.Collections.Generic;

namespace COSGeniusSystem.DTOs;

public class ReportFilterRequest
{
    public DateTime? FromDate { get; set; }
    public DateTime? ToDate { get; set; }
    public int? SupplierId { get; set; }
    public string? Status { get; set; }
    public string? Keyword { get; set; }
}

// ==========================================
// BÁO CÁO NHẬP KHO (STOCK IMPORT REPORT)
// ==========================================

public class StockImportReportDto
{
    public DateTime? FromDate { get; set; }
    public DateTime? ToDate { get; set; }

    /// <summary>
    /// Nhập bao nhiêu đơn rồi: Tổng số phiếu / đơn nhập kho trong kỳ
    /// </summary>
    public int TotalOrders { get; set; }

    /// <summary>
    /// Tốn bao nhiêu tiền: Tổng chi phí nhập kho (VNĐ)
    /// </summary>
    public decimal TotalSpent { get; set; }

    /// <summary>
    /// Tổng khối lượng / số lượng dược liệu nhập kho (gam / đơn vị)
    /// </summary>
    public decimal TotalQuantityGrams { get; set; }

    /// <summary>
    /// Tổng số nhà cung cấp đã nhập hàng
    /// </summary>
    public int TotalSuppliers { get; set; }

    /// <summary>
    /// Thống kê chi tiết theo từng Nhà cung cấp: Nhập vị thuốc nào, tốn bao nhiêu tiền
    /// </summary>
    public List<SupplierImportSummaryDto> BySupplier { get; set; } = new();

    /// <summary>
    /// Thống kê chi tiết theo từng Vị thuốc: Nhập bao nhiêu, của NCC nào, tốn bao nhiêu
    /// </summary>
    public List<HerbImportSummaryDto> ByHerb { get; set; } = new();

    /// <summary>
    /// Danh sách chi tiết các phiếu nhập kho
    /// </summary>
    public List<ImportReceiptItemDto> Receipts { get; set; } = new();
}

public class SupplierImportSummaryDto
{
    public int SupplierId { get; set; }
    public string SupplierCode { get; set; } = string.Empty;
    public string SupplierName { get; set; } = string.Empty;
    public int OrderCount { get; set; }
    public decimal TotalSpent { get; set; }
    public decimal TotalQuantityGrams { get; set; }
    public decimal PercentOfTotalSpent { get; set; }
    public List<string> SuppliedHerbNames { get; set; } = new();
}

public class HerbImportSummaryDto
{
    public int HerbId { get; set; }
    public string HerbCode { get; set; } = string.Empty;
    public string HerbName { get; set; } = string.Empty;
    public string Unit { get; set; } = string.Empty;
    public decimal TotalQuantity { get; set; }
    public decimal TotalSpent { get; set; }
    public decimal AverageUnitPrice { get; set; }
    public List<string> Suppliers { get; set; } = new();
}

public class ImportReceiptItemDto
{
    public int ReceiptId { get; set; }
    public string ReceiptCode { get; set; } = string.Empty;
    public DateTime ReceiptDate { get; set; }
    public string? PoCode { get; set; }
    public string SupplierName { get; set; } = string.Empty;
    public string ReceiverName { get; set; } = string.Empty;
    public string WarehouseName { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string StatusLabel { get; set; } = string.Empty;
    public decimal TotalCost { get; set; }
    public decimal TotalQuantity { get; set; }
    public string? Note { get; set; }
    public List<ImportReceiptDetailItemDto> Details { get; set; } = new();
}

public class ImportReceiptDetailItemDto
{
    public int HerbId { get; set; }
    public string HerbCode { get; set; } = string.Empty;
    public string HerbName { get; set; } = string.Empty;
    public string BatchNumber { get; set; } = string.Empty;
    public decimal Quantity { get; set; }
    public string Unit { get; set; } = "g";
    public decimal UnitPrice { get; set; }
    public decimal LineTotal { get; set; }
    public string ExpiryDate { get; set; } = string.Empty;
    public string? Location { get; set; }
}

// ==========================================
// BÁO CÁO XUẤT KHO (STOCK EXPORT & SALES REPORT)
// ==========================================

public class StockExportReportDto
{
    public DateTime? FromDate { get; set; }
    public DateTime? ToDate { get; set; }

    /// <summary>
    /// Số lượng bệnh nhân mua thuốc / phát sinh đơn trong kỳ
    /// </summary>
    public int TotalPatients { get; set; }

    /// <summary>
    /// Bao nhiêu đơn thuốc được bán ra: Tổng số đơn thuốc xuất bán
    /// </summary>
    public int TotalPrescriptionsSold { get; set; }

    /// <summary>
    /// Tổng số thang thuốc bán ra
    /// </summary>
    public int TotalDosesSold { get; set; }

    /// <summary>
    /// Bao nhiêu đơn vị thuốc được bán ra: Tổng lượng/đơn vị vị thuốc đã xuất bán (gam)
    /// </summary>
    public decimal TotalHerbUnitsSold { get; set; }

    /// <summary>
    /// Thu được bao nhiêu tiền: Tổng doanh thu bán thuốc
    /// </summary>
    public decimal TotalRevenue { get; set; }

    /// <summary>
    /// Tổng chi phí giá vốn của lượng thuốc đã xuất bán
    /// </summary>
    public decimal TotalCost { get; set; }

    /// <summary>
    /// Lãi thực tế được bao nhiêu tiền: Doanh thu - Giá vốn
    /// </summary>
    public decimal ActualProfit { get; set; }

    /// <summary>
    /// Tỷ suất lợi nhuận thực tế (%): ActualProfit / TotalRevenue * 100
    /// </summary>
    public decimal ProfitMargin { get; set; }

    /// <summary>
    /// Thống kê chi tiết theo từng Vị thuốc xuất bán: số lượng, doanh thu, vốn, lãi
    /// </summary>
    public List<HerbExportSummaryDto> ByHerb { get; set; } = new();

    /// <summary>
    /// Thống kê chi tiết theo từng Bệnh nhân: số đơn, doanh thu, lãi
    /// </summary>
    public List<PatientExportSummaryDto> ByPatient { get; set; } = new();

    /// <summary>
    /// Danh sách chi tiết các đơn thuốc bán ra
    /// </summary>
    public List<PrescriptionSaleItemDto> Prescriptions { get; set; } = new();

    /// <summary>
    /// Thống kê xu hướng doanh thu và lợi nhuận theo ngày
    /// </summary>
    public List<DailyExportTrendDto> DailyTrends { get; set; } = new();
}

public class HerbExportSummaryDto
{
    public int HerbId { get; set; }
    public string HerbCode { get; set; } = string.Empty;
    public string HerbName { get; set; } = string.Empty;
    public string Unit { get; set; } = "g";
    public decimal TotalQuantitySold { get; set; }
    public decimal TotalRevenue { get; set; }
    public decimal TotalCost { get; set; }
    public decimal ActualProfit { get; set; }
    public decimal ProfitMargin { get; set; }
    public int PrescriptionCount { get; set; }
}

public class PatientExportSummaryDto
{
    public string PatientCode { get; set; } = string.Empty;
    public string PatientName { get; set; } = string.Empty;
    public int PrescriptionCount { get; set; }
    public int TotalDoses { get; set; }
    public decimal TotalRevenue { get; set; }
    public decimal TotalCost { get; set; }
    public decimal TotalProfit { get; set; }
    public decimal ProfitMargin { get; set; }
}

public class PrescriptionSaleItemDto
{
    public int PrescriptionId { get; set; }
    public string PrescriptionCode { get; set; } = string.Empty;
    public string PrescriptionName { get; set; } = string.Empty;
    public string PatientName { get; set; } = string.Empty;
    public string PatientCode { get; set; } = string.Empty;
    public string DoctorName { get; set; } = string.Empty;
    public DateTime SoldDate { get; set; }
    public int TotalDoses { get; set; }
    public string Status { get; set; } = string.Empty;
    public string StatusLabel { get; set; } = string.Empty;
    public string? InvoiceCode { get; set; }
    public string PaymentMethod { get; set; } = string.Empty;
    public decimal Revenue { get; set; }
    public decimal Cost { get; set; }
    public decimal Profit { get; set; }
    public decimal ProfitMargin { get; set; }
    public List<PrescriptionSaleDetailItemDto> Details { get; set; } = new();
}

public class PrescriptionSaleDetailItemDto
{
    public int HerbId { get; set; }
    public string HerbCode { get; set; } = string.Empty;
    public string HerbName { get; set; } = string.Empty;
    public decimal DosagePerDose { get; set; }
    public decimal TotalQuantity { get; set; }
    public string Unit { get; set; } = "g";
    public decimal SellingPrice { get; set; }
    public decimal CostPrice { get; set; }
    public decimal SubTotalRevenue { get; set; }
    public decimal SubTotalCost { get; set; }
    public decimal LineProfit { get; set; }
    public decimal ProfitMargin { get; set; }
}

public class DailyExportTrendDto
{
    public string Date { get; set; } = string.Empty;
    public int PrescriptionsCount { get; set; }
    public decimal Revenue { get; set; }
    public decimal Cost { get; set; }
    public decimal Profit { get; set; }
}


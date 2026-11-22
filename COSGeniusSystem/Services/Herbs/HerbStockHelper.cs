using COSGeniusSystem.Data;
using Microsoft.EntityFrameworkCore;

namespace COSGeniusSystem.Services.Herbs;

public sealed record HerbStockInfo(decimal PhysicalStock, decimal ReservedStock, decimal AvailableStock);

public static class HerbStockHelper
{
    public static async Task<Dictionary<int, HerbStockInfo>> GetStockMapAsync(
        ApplicationDbContext db,
        IEnumerable<int> herbIds,
        int? excludePrescriptionId = null,
        CancellationToken ct = default)
    {
        var ids = herbIds.Where(id => id > 0).Distinct().ToList();
        if (ids.Count == 0) return new Dictionary<int, HerbStockInfo>();

        var today = DateOnly.FromDateTime(DateTime.UtcNow.AddHours(7));

        // 1. Tồn kho thực tế (Physical Stock):
        // Các lô đạt chuẩn, còn hạn dùng và CurrentQuantity > 0
        var physicalStock = await db.Batches.AsNoTracking()
            .Where(b => ids.Contains(b.HerbId)
                     && b.CurrentQuantity > 0
                     && (b.Status == "Đạt chuẩn" || b.Status == "Active" || b.Status == "ACTIVE" || string.IsNullOrEmpty(b.Status))
                     && (b.ExpiryDate >= today || b.ExpiryDate == default))
            .GroupBy(b => b.HerbId)
            .Select(g => new { HerbId = g.Key, Total = g.Sum(b => b.CurrentQuantity) })
            .ToDictionaryAsync(x => x.HerbId, x => x.Total, ct);

        // 2. Lượng giữ chỗ (Reserved Stock):
        // Tổng lượng thuốc đã kê trong các đơn đang ở trạng thái đã gửi/chờ xử lý (SENT hoặc PENDING)
        // nhưng chưa xuất kho thực tế (chưa DISPENSED và không có MedicineDispense completed) và không bị CANCELLED.
        var reservedStock = await db.HerbalPrescriptionDetails.AsNoTracking()
            .Where(d => ids.Contains(d.HerbId)
                     && (d.Prescription.Status == "SENT" || d.Prescription.Status == "PENDING")
                     && d.Prescription.Status != "DISPENSED"
                     && d.Prescription.Status != "CANCELLED"
                     && (d.Prescription.MedicineDispense == null || d.Prescription.MedicineDispense.Status != "COMPLETED")
                     && (excludePrescriptionId == null || d.PrescriptionId != excludePrescriptionId.Value)
                     && d.DosagePerDose > 0
                     && d.Prescription.TotalDoses > 0)
            .GroupBy(d => d.HerbId)
            .Select(g => new { HerbId = g.Key, Total = g.Sum(d => d.DosagePerDose * d.Prescription.TotalDoses) })
            .ToDictionaryAsync(x => x.HerbId, x => x.Total, ct);

        var result = new Dictionary<int, HerbStockInfo>();
        foreach (var id in ids)
        {
            var phys = physicalStock.GetValueOrDefault(id, 0m);
            var res = reservedStock.GetValueOrDefault(id, 0m);
            var avail = Math.Max(0m, phys - res);
            result[id] = new HerbStockInfo(phys, res, avail);
        }
        return result;
    }
}

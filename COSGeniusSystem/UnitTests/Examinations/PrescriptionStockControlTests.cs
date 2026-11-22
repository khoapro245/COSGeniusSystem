using System.Security.Claims;
using COSGeniusSystem.Commons;
using COSGeniusSystem.DTOs;
using COSGeniusSystem.Exceptions;
using COSGeniusSystem.Models;
using COSGeniusSystem.Services;
using COSGeniusSystem.Services.Herbs;

namespace COSGeniusSystem.UnitTests;

public class PrescriptionStockControlTests
{
    private static ClaimsPrincipal DoctorActor(int id = 1) => new(new ClaimsIdentity(new[]
    {
        new Claim(ClaimTypes.NameIdentifier, id.ToString()),
        new Claim(ClaimTypes.Role, RoleConstants.Doctor)
    }, "test"));

    [Fact]
    public async Task DynamicAvailableStock_CalculatesPhysicalMinusReserved()
    {
        using var db = SchemaFixture.Create();
        var herb = new Herb { HerbId = 1, HerbCode = "H01", CommonName = "Cam Thảo", Unit = "g", StandardPrice = 1000m, IsActive = true };
        var batch = new Batch { BatchId = 1, HerbId = 1, BatchNumber = "B01", CurrentQuantity = 100m, OriginalQuantity = 100m, ExpiryDate = DateOnly.FromDateTime(DateTime.UtcNow.AddYears(1)), Status = "Đạt chuẩn" };
        db.Herbs.Add(herb);
        db.Batches.Add(batch);

        var record1 = SchemaFixture.Record(1);
        record1.Doctor.User!.IsActive = true;
        // Đơn 1 ở trạng thái SENT: 5 thang * 6g = 30g
        var rx1 = new HerbalPrescription
        {
            PrescriptionId = 1, Record = record1, CreatedByNavigation = record1.Doctor.User,
            Status = "SENT", TotalDoses = 5, UsageInstructions = "Sắc uống",
            HerbalPrescriptionDetails = [new() { HerbId = 1, DosagePerDose = 6m, HerbNameSnapshot = "Cam Thảo" }]
        };

        // Đơn 2 ở trạng thái DRAFT: 2 thang * 10g = 20g (Nháp không được tính vào lượng giữ chỗ)
        var record2 = SchemaFixture.Record(2);
        record2.Doctor.User!.IsActive = true;
        var rx2 = new HerbalPrescription
        {
            PrescriptionId = 2, Record = record2, CreatedByNavigation = record2.Doctor.User,
            Status = "DRAFT", TotalDoses = 2, UsageInstructions = "Sắc uống",
            HerbalPrescriptionDetails = [new() { HerbId = 1, DosagePerDose = 10m, HerbNameSnapshot = "Cam Thảo" }]
        };

        db.MedicalRecords.AddRange(record1, record2);
        db.HerbalPrescriptions.AddRange(rx1, rx2);
        await db.SaveChangesAsync();

        // Tồn kho: 100g, Đã giữ chỗ: 30g, Khả dụng: 70g
        var stockMap = await HerbStockHelper.GetStockMapAsync(db, new[] { 1 });
        Assert.Equal(100m, stockMap[1].PhysicalStock);
        Assert.Equal(30m, stockMap[1].ReservedStock);
        Assert.Equal(70m, stockMap[1].AvailableStock);

        // Khi kiểm tra tồn cho chính đơn 1 (để sửa đơn 1), không tính giữ chỗ của chính nó
        var stockMapExcludeRx1 = await HerbStockHelper.GetStockMapAsync(db, new[] { 1 }, excludePrescriptionId: 1);
        Assert.Equal(0m, stockMapExcludeRx1[1].ReservedStock);
        Assert.Equal(100m, stockMapExcludeRx1[1].AvailableStock);
    }

    [Fact]
    public async Task SendPrescription_ThrowsConflict_WhenHerbExceedsAvailableStock()
    {
        using var db = SchemaFixture.Create();
        var record = SchemaFixture.Record(1);
        record.Doctor.User!.IsActive = true;
        record.Status = record.Registration.Status = "IN_PROGRESS";
        record.Symptoms = "Đau đầu";
        record.FinalDiagnosis = "Nhiệt chứng";

        var herb = new Herb { HerbId = 1, HerbCode = "H01", CommonName = "Hoàng Cầm", Unit = "g", StandardPrice = 2000m, IsActive = true };
        // Tồn kho chỉ có 20g
        var batch = new Batch { BatchId = 1, HerbId = 1, BatchNumber = "B01", CurrentQuantity = 20m, OriginalQuantity = 20m, ExpiryDate = DateOnly.FromDateTime(DateTime.UtcNow.AddYears(1)), Status = "Đạt chuẩn" };

        var rx = new HerbalPrescription
        {
            PrescriptionId = 1, Record = record, CreatedByNavigation = record.Doctor.User,
            Status = "DRAFT", TotalDoses = 3, UsageInstructions = "Sắc uống ngày 2 lần",
            HerbalPrescriptionDetails = [new() { HerbId = 1, DosagePerDose = 10m, HerbNameSnapshot = "Hoàng Cầm" }] // Cần 3 * 10g = 30g > 20g
        };

        db.MedicalRecords.Add(record);
        db.Herbs.Add(herb);
        db.Batches.Add(batch);
        db.HerbalPrescriptions.Add(rx);
        await db.SaveChangesAsync();

        var service = new ExaminationService(db);
        var ex = await Assert.ThrowsAsync<ConflictException>(() =>
            service.SendPrescriptionAsync(DoctorActor(), 1, 1, new VersionRequest { RowVersion = rx.RowVersion }, default));

        Assert.Contains("Hoàng Cầm", ex.Message);
        Assert.Contains("30", ex.Message);
        Assert.Contains("20", ex.Message);
    }

    [Fact]
    public async Task SendPrescription_Succeeds_WhenHerbStockIsSufficient()
    {
        using var db = SchemaFixture.Create();
        var record = SchemaFixture.Record(1);
        record.Doctor.User!.IsActive = true;
        record.Status = record.Registration.Status = "IN_PROGRESS";
        record.Symptoms = "Mất ngủ";
        record.FinalDiagnosis = "Tâm tỳ lưỡng hư";

        var herb = new Herb { HerbId = 1, HerbCode = "H02", CommonName = "Toan Táo Nhân", Unit = "g", StandardPrice = 3500m, IsActive = true };
        // Tồn kho 50g
        var batch = new Batch { BatchId = 1, HerbId = 1, BatchNumber = "B02", CurrentQuantity = 50m, OriginalQuantity = 50m, ExpiryDate = DateOnly.FromDateTime(DateTime.UtcNow.AddYears(1)), Status = "Đạt chuẩn" };

        var rx = new HerbalPrescription
        {
            PrescriptionId = 1, Record = record, CreatedByNavigation = record.Doctor.User,
            Status = "DRAFT", TotalDoses = 2, UsageInstructions = "Sắc uống trước khi ngủ",
            HerbalPrescriptionDetails = [new() { HerbId = 1, DosagePerDose = 15m, HerbNameSnapshot = "Toan Táo Nhân" }] // Cần 2 * 15g = 30g <= 50g
        };

        db.MedicalRecords.Add(record);
        db.Herbs.Add(herb);
        db.Batches.Add(batch);
        db.HerbalPrescriptions.Add(rx);
        await db.SaveChangesAsync();

        var service = new ExaminationService(db);
        var result = await service.SendPrescriptionAsync(DoctorActor(), 1, 1, new VersionRequest { RowVersion = rx.RowVersion }, default);

        Assert.Equal("SENT", result.Status);
        Assert.Single(result.Items);
        Assert.Equal(3500m, result.Items[0].UnitPrice);
        // Sau khi đơn 1 được gửi (giữ 30g), khả dụng cho đơn khác là 50 - 30 = 20g,
        // nhưng với đơn 1 được loại trừ, nó hiển thị 50g
        Assert.True(result.Items[0].AvailableStock > 0);

        // Kiểm tra đối với bên ngoài (không loại trừ đơn 1): khả dụng chỉ còn 20g
        var stockMap = await HerbStockHelper.GetStockMapAsync(db, new[] { 1 });
        Assert.Equal(20m, stockMap[1].AvailableStock);
    }

    [Fact]
    public async Task CompleteExamination_SendsPrescriptionAndTransitionsStatus_WhenStockSufficient()
    {
        using var db = SchemaFixture.Create();
        var record = SchemaFixture.Record(1);
        record.Doctor.User!.IsActive = true;
        record.Status = record.Registration.Status = "IN_PROGRESS";
        record.Symptoms = "Đau mỏi vai gáy";
        record.FinalDiagnosis = "Hội chứng vai gáy thể phong hàn";

        var herb = new Herb { HerbId = 1, HerbCode = "H03", CommonName = "Ngải Diệp", Unit = "g", StandardPrice = 1200m, IsActive = true };
        var batch = new Batch { BatchId = 1, HerbId = 1, BatchNumber = "B03", CurrentQuantity = 100m, OriginalQuantity = 100m, ExpiryDate = DateOnly.FromDateTime(DateTime.UtcNow.AddYears(1)), Status = "Đạt chuẩn" };

        var rx = new HerbalPrescription
        {
            PrescriptionId = 1, Record = record, CreatedByNavigation = record.Doctor.User,
            Status = "DRAFT", TotalDoses = 3, UsageInstructions = "Sắc uống ấm",
            HerbalPrescriptionDetails = [new() { HerbId = 1, DosagePerDose = 10m, HerbNameSnapshot = "Ngải Diệp" }] // Cần 30g <= 100g
        };

        db.MedicalRecords.Add(record);
        db.Herbs.Add(herb);
        db.Batches.Add(batch);
        db.HerbalPrescriptions.Add(rx);
        await db.SaveChangesAsync();

        var service = new ExaminationService(db);
        // Hoàn tất khám tại tab Kết quả / Hoàn tất
        var completed = await service.CompleteAsync(DoctorActor(), 1, new VersionRequest { RowVersion = record.RowVersion }, default);

        Assert.Equal("COMPLETED", completed.Status);
        Assert.Equal("SENT", rx.Status); // Đơn thuốc tự động được chuyển sang SENT khi hoàn tất ca khám
        Assert.NotNull(rx.SentAt);

        // Sau khi hoàn tất khám, 30g đã được giữ chỗ trong kho
        var stockMap = await HerbStockHelper.GetStockMapAsync(db, new[] { 1 });
        Assert.Equal(70m, stockMap[1].AvailableStock);
    }

    [Fact]
    public async Task CompleteExamination_BlocksAndRollsBack_WhenPrescriptionHerbExceedsStock()
    {
        using var db = SchemaFixture.Create();
        var record = SchemaFixture.Record(1);
        record.Doctor.User!.IsActive = true;
        record.Status = record.Registration.Status = "IN_PROGRESS";
        record.Symptoms = "Chóng mặt";
        record.FinalDiagnosis = "Huyễn vựng";

        var herb = new Herb { HerbId = 1, HerbCode = "H04", CommonName = "Kỷ Tử", Unit = "g", StandardPrice = 4000m, IsActive = true };
        // Kho chỉ còn 15g
        var batch = new Batch { BatchId = 1, HerbId = 1, BatchNumber = "B04", CurrentQuantity = 15m, OriginalQuantity = 15m, ExpiryDate = DateOnly.FromDateTime(DateTime.UtcNow.AddYears(1)), Status = "Đạt chuẩn" };

        var rx = new HerbalPrescription
        {
            PrescriptionId = 1, Record = record, CreatedByNavigation = record.Doctor.User,
            Status = "DRAFT", TotalDoses = 2, UsageInstructions = "Sắc uống",
            HerbalPrescriptionDetails = [new() { HerbId = 1, DosagePerDose = 10m, HerbNameSnapshot = "Kỷ Tử" }] // Cần 20g > 15g
        };

        db.MedicalRecords.Add(record);
        db.Herbs.Add(herb);
        db.Batches.Add(batch);
        db.HerbalPrescriptions.Add(rx);
        await db.SaveChangesAsync();

        var service = new ExaminationService(db);
        var ex = await Assert.ThrowsAsync<ConflictException>(() =>
            service.CompleteAsync(DoctorActor(), 1, new VersionRequest { RowVersion = record.RowVersion }, default));

        Assert.Contains("Kỷ Tử", ex.Message);
        Assert.Contains("20", ex.Message);
        Assert.Contains("15", ex.Message);

        // Đơn thuốc vẫn giữ nguyên trạng thái DRAFT, ca khám vẫn là IN_PROGRESS do transaction rollback
        Assert.Equal("DRAFT", rx.Status);
        Assert.Equal("IN_PROGRESS", record.Status);
    }
}

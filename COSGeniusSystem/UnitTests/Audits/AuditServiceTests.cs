using COSGeniusSystem.Data;
using COSGeniusSystem.DTOs;
using COSGeniusSystem.Models;
using COSGeniusSystem.Services;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace COSGeniusSystem.UnitTests;

public class AuditServiceTests
{
    private static ApplicationDbContext CreateInMemoryDbContext()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(databaseName: $"ClinicHerbDb_Audit_{Guid.NewGuid()}")
            .Options;

        return new ApplicationDbContext(options);
    }

    [Fact]
    public async Task CreateAuditAsync_CreatesScheduledAuditAndSnapshotsBatches()
    {
        // Arrange
        using var context = CreateInMemoryDbContext();
        var wh = new Warehouse { WarehouseId = 1, WarehouseCode = "KHO-01", WarehouseName = "Kho Trung Tâm", Location = "Tầng 1" };
        var user = new User { UserId = 1006, Username = "inventory", PasswordHash = "hash", FullName = "Trần Văn Bình", IsActive = true, CreatedAt = DateTime.UtcNow };
        var herb = new Herb { HerbId = 1, HerbCode = "VT001", CommonName = "Cam thảo", Unit = "kg", StandardPrice = 100000m, MinThreshold = 50m, IsActive = true };
        var batch = new Batch { BatchId = 10, BatchNumber = "LO-CT-01", HerbId = 1, WarehouseId = 1, CurrentQuantity = 15m, OriginalQuantity = 20m, ImportDate = DateTime.UtcNow, ExpiryDate = DateOnly.FromDateTime(DateTime.UtcNow.AddYears(1)), UnitCost = 90000m, Status = "Đạt chuẩn" };

        context.Warehouses.Add(wh);
        context.Users.Add(user);
        context.Herbs.Add(herb);
        context.Batches.Add(batch);
        // A completed audit prevents the service demo seed from creating an open audit.
        context.InventoryAudits.Add(new InventoryAudit { AuditCode = "KK00001", WarehouseId = 1, AuditedBy = 1006, AuditDate = DateTime.UtcNow, Status = "Đã duyệt" });
        await context.SaveChangesAsync();

        var service = new AuditService(context);

        // Act
        var result = await service.CreateAuditAsync(new CreateAuditRequest
        {
            WarehouseId = 1,
            AuditedBy = 1006,
            Note = "Kiểm kê định kỳ tháng 11/2026"
        });

        // Assert
        Assert.NotNull(result);
        Assert.StartsWith("KK", result.AuditCode);
        Assert.Equal("Chờ kiểm kê", result.Status);
        Assert.Single(result.Details);
        Assert.Equal(15m, result.Details[0].SystemQuantity);
        Assert.Equal(15m, result.Details[0].ActualQuantity);
    }

    [Fact]
    public async Task UpdateAuditCountAsync_UpdatesActualQuantityAndSetsPendingApproval()
    {
        // Arrange
        using var context = CreateInMemoryDbContext();
        var wh = new Warehouse { WarehouseId = 1, WarehouseCode = "KHO-01", WarehouseName = "Kho Trung Tâm", Location = "Tầng 1" };
        var user = new User { UserId = 1006, Username = "inventory", PasswordHash = "hash", FullName = "Trần Văn Bình", IsActive = true, CreatedAt = DateTime.UtcNow };
        var herb = new Herb { HerbId = 1, HerbCode = "VT001", CommonName = "Cam thảo", Unit = "kg", StandardPrice = 100000m, MinThreshold = 50m, IsActive = true };
        var batch = new Batch { BatchId = 10, BatchNumber = "LO-CT-01", HerbId = 1, WarehouseId = 1, CurrentQuantity = 15m, OriginalQuantity = 20m, ImportDate = DateTime.UtcNow, ExpiryDate = DateOnly.FromDateTime(DateTime.UtcNow.AddYears(1)), UnitCost = 90000m, Status = "Đạt chuẩn" };

        context.Warehouses.Add(wh);
        context.Users.Add(user);
        context.Herbs.Add(herb);
        context.Batches.Add(batch);
        // A completed audit prevents the service demo seed from creating an open audit.
        context.InventoryAudits.Add(new InventoryAudit { AuditCode = "KK00001", WarehouseId = 1, AuditedBy = 1006, AuditDate = DateTime.UtcNow, Status = "Đã duyệt" });
        await context.SaveChangesAsync();

        var service = new AuditService(context);
        var created = await service.CreateAuditAsync(new CreateAuditRequest { WarehouseId = 1, AuditedBy = 1006 });

        // Act: Inventory staff counts 14.8 kg and completes audit
        var updated = await service.UpdateAuditCountAsync(created.AuditCode, new UpdateAuditCountRequest
        {
            IsComplete = true,
            Note = "Đã kiểm đếm xong thực tế",
            Items = new List<UpdateAuditCountItem>
            {
                new UpdateAuditCountItem { BatchId = 10, ActualQuantity = 14.8m, Reason = "Hao hụt tự nhiên" }
            }
        });

        // Assert
        Assert.Equal("Chờ duyệt", updated.Status);
        Assert.Single(updated.Details);
        Assert.Equal(14.8m, updated.Details[0].ActualQuantity);
        Assert.Equal(-0.2m, updated.Details[0].Discrepancy);
        Assert.Equal("Điều chỉnh giảm", updated.Details[0].ResolutionStatus);
    }

    [Fact]
    public async Task ApproveAuditAdjustmentAsync_SynchronizesBatchCurrentQuantity()
    {
        // Arrange
        using var context = CreateInMemoryDbContext();
        var wh = new Warehouse { WarehouseId = 1, WarehouseCode = "KHO-01", WarehouseName = "Kho Trung Tâm", Location = "Tầng 1" };
        var user = new User { UserId = 1006, Username = "inventory", PasswordHash = "hash", FullName = "Trần Văn Bình", IsActive = true, CreatedAt = DateTime.UtcNow };
        var herb = new Herb { HerbId = 1, HerbCode = "VT001", CommonName = "Cam thảo", Unit = "kg", StandardPrice = 100000m, MinThreshold = 50m, IsActive = true };
        var batch = new Batch { BatchId = 10, BatchNumber = "LO-CT-01", HerbId = 1, WarehouseId = 1, CurrentQuantity = 15m, OriginalQuantity = 20m, ImportDate = DateTime.UtcNow, ExpiryDate = DateOnly.FromDateTime(DateTime.UtcNow.AddYears(1)), UnitCost = 90000m, Status = "Đạt chuẩn" };

        context.Warehouses.Add(wh);
        context.Users.Add(user);
        context.Herbs.Add(herb);
        context.Batches.Add(batch);
        // A completed audit prevents the service demo seed from creating an open audit.
        context.InventoryAudits.Add(new InventoryAudit { AuditCode = "KK00001", WarehouseId = 1, AuditedBy = 1006, AuditDate = DateTime.UtcNow, Status = "Đã duyệt" });
        await context.SaveChangesAsync();

        var service = new AuditService(context);
        var created = await service.CreateAuditAsync(new CreateAuditRequest { WarehouseId = 1, AuditedBy = 1006 });

        await service.UpdateAuditCountAsync(created.AuditCode, new UpdateAuditCountRequest
        {
            IsComplete = true,
            Items = new List<UpdateAuditCountItem>
            {
                new UpdateAuditCountItem { BatchId = 10, ActualQuantity = 14.8m }
            }
        });

        // Act: Manager approves stock adjustment
        var approved = await service.ApproveAuditAdjustmentAsync(created.AuditCode, new ApproveAuditRequest
        {
            ManagerOpinion = "Đồng ý điều chỉnh theo biên bản kiểm đếm"
        });

        // Assert
        Assert.Equal("Đã duyệt", approved.Status);
        var dbBatch = await context.Batches.FindAsync(10);
        Assert.NotNull(dbBatch);
        Assert.Equal(14.8m, dbBatch.CurrentQuantity); // Batch is updated to actual count!
    }

    [Fact]
    public async Task UpdateAuditCountAsync_WithDestruction_CalculatesUsableDiscrepancyCorrectly()
    {
        // Arrange: Batch system stock = 8790g, counted = 9000g, destroy = 200g => usable = 8800g, diff = +10g
        using var context = CreateInMemoryDbContext();
        var wh = new Warehouse { WarehouseId = 1, WarehouseCode = "KHO-01", WarehouseName = "Kho Trung Tâm", Location = "Tầng 1" };
        var user = new User { UserId = 1006, Username = "inventory", PasswordHash = "hash", FullName = "Trần Văn Bình", IsActive = true, CreatedAt = DateTime.UtcNow };
        var herb = new Herb { HerbId = 1, HerbCode = "G79DEMO_H01", CommonName = "Cam thảo", Unit = "g", StandardPrice = 210m, MinThreshold = 500m, IsActive = true };
        var batch = new Batch { BatchId = 101, BatchNumber = "G79DEMO_BATCH01", HerbId = 1, WarehouseId = 1, CurrentQuantity = 8790m, OriginalQuantity = 10000m, ImportDate = DateTime.UtcNow, ExpiryDate = DateOnly.FromDateTime(DateTime.UtcNow.AddYears(1)), UnitCost = 210m, Status = "Đạt chuẩn" };

        context.Warehouses.Add(wh);
        context.Users.Add(user);
        context.Herbs.Add(herb);
        context.Batches.Add(batch);
        // A completed audit prevents the service demo seed from creating an open audit.
        context.InventoryAudits.Add(new InventoryAudit { AuditCode = "KK00001", WarehouseId = 1, AuditedBy = 1006, AuditDate = DateTime.UtcNow, Status = "Đã duyệt" });
        await context.SaveChangesAsync();

        var service = new AuditService(context);
        var created = await service.CreateAuditAsync(new CreateAuditRequest { WarehouseId = 1, AuditedBy = 1006 });

        // Act: Count 9000g with 200g destroy
        var updated = await service.UpdateAuditCountAsync(created.AuditCode, new UpdateAuditCountRequest
        {
            IsComplete = true,
            Items = new List<UpdateAuditCountItem>
            {
                new UpdateAuditCountItem
                {
                    BatchId = 101,
                    ActualQuantity = 9000m,
                    Reason = "Giảm tồn [Tiêu hủy: 200 g]"
                }
            }
        });

        // Assert:
        // System = 8790, Count = 9000, Destroy = 200 => Usable = 8800
        // Discrepancy = Usable - System = 8800 - 8790 = +10
        Assert.Single(updated.Details);
        var detail = updated.Details[0];
        Assert.Equal(8790m, detail.SystemQuantity);
        Assert.Equal(9000m, detail.ActualQuantity);
        Assert.Equal(10m, detail.Discrepancy);
        Assert.Equal("Điều chỉnh tăng", detail.ResolutionStatus);

        // Approve and verify batch stock is set to usable 8800g
        var approved = await service.ApproveAuditAdjustmentAsync(created.AuditCode, new ApproveAuditRequest
        {
            ManagerOpinion = "Đã duyệt cân bằng kiểm kê"
        });
        var dbBatch = await context.Batches.FindAsync(101);
        Assert.NotNull(dbBatch);
        Assert.Equal(8800m, dbBatch.CurrentQuantity);
    }
    [Fact]
    public async Task CreateAuditAsync_BlocksFullWarehouseWhenAuditIsOpen()
    {
        using var context = CreateInMemoryDbContext();
        context.Warehouses.Add(new Warehouse { WarehouseId = 1, WarehouseCode = "K", WarehouseName = "Kho", Location = "A" });
        context.Users.Add(new User { UserId = 1006, Username = "inventory", PasswordHash = "hash", FullName = "Nhân viên", IsActive = true });
        context.InventoryAudits.Add(new InventoryAudit { AuditCode = "KK00008", WarehouseId = 1, AuditedBy = 1006, AuditDate = DateTime.UtcNow, Status = "Chờ duyệt" });
        await context.SaveChangesAsync();
        var error = await Assert.ThrowsAsync<COSGeniusSystem.Exceptions.BadRequestException>(() =>
            new AuditService(context).CreateAuditAsync(new CreateAuditRequest { WarehouseId = 1, AuditedBy = 1006 }));
        Assert.Contains("KK00008", error.Message);
        Assert.Single(await context.InventoryAudits.ToListAsync());
    }
}


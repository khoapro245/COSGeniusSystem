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
}


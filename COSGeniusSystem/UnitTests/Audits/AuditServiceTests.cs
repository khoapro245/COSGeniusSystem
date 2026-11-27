using COSGeniusSystem.Data;
using COSGeniusSystem.DTOs;
using COSGeniusSystem.Exceptions;
using COSGeniusSystem.Models;
using COSGeniusSystem.Services;
using Microsoft.EntityFrameworkCore;

public class AuditServiceTests : IDisposable
{
    private readonly ApplicationDbContext _context;
    private readonly AuditService _service;
    private const string Code = "KK00008";

    public AuditServiceTests()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options;
        _context = new ApplicationDbContext(options);
        _service = new AuditService(_context);
    }

    private async Task SeedAsync(string status, decimal actual = 100m,
        decimal stock = 100m, string? reason = null)
    {
        _context.Warehouses.Add(new Warehouse { WarehouseId = 1, WarehouseCode = "WH001", WarehouseName = "Test warehouse" });
        _context.Users.Add(new User { UserId = 1, Username = "staff01", FullName = "Test Staff", PasswordHash = "test_hash", IsActive = true, CreatedAt = DateTime.UtcNow });
        // Target-typed new avoids assuming the batch entity class name.
        _context.Batches.Add(new()
        {
            BatchId = 1,
            BatchNumber = "B001",
            WarehouseId = 1,
            HerbId = 1,
            Herb = new()
            {
                HerbId = 1,
                HerbCode = "VT001",
                CommonName = "Test herb",
                Unit = "kg"
            },
            CurrentQuantity = stock,
            Status = "Active"
        });
        _context.InventoryAudits.Add(new InventoryAudit { AuditId = 1, AuditCode = Code, WarehouseId = 1, AuditedBy = 1, AuditDate = DateTime.UtcNow, Status = status, Note = "[Phạm vi: Toàn kho]" });
        _context.AuditDetails.Add(new AuditDetail { AuditId = 1, BatchId = 1, SystemQuantity = 100m, ActualQuantity = actual, Reason = reason });
        await _context.SaveChangesAsync();
        _context.ChangeTracker.Clear();
    }

    private static CreateAuditRequest CreateRequest(int warehouseId = 1) => new()
    { WarehouseId = warehouseId, Scope = "Toàn kho" };

    private static UpdateAuditCountRequest CountRequest(decimal quantity, bool complete) => new()
    { IsComplete = complete, Items = new() { new() { BatchId = 1, ActualQuantity = quantity } } };

    private async Task<decimal> StockAsync()
    {
        _context.ChangeTracker.Clear();
        return (await _context.Batches.SingleAsync(b => b.BatchId == 1)).CurrentQuantity;
    }

    // UTC-AUDIT-001
    [Fact]
    public async Task CreateAuditAsync_ValidRequest_CreatesAudit()
    {
        // Existing completed audit prevents automatic demo seeding.
        await SeedAsync("Đã duyệt");
        var result = await _service.CreateAuditAsync(CreateRequest(), 1);
        Assert.Equal("KK00009", result.AuditCode);
        Assert.Equal("Chờ kiểm kê", result.Status);
        Assert.Equal(1, result.AuditedBy);
        var detail = Assert.Single(result.Details);
        Assert.Equal(100m, detail.SystemQuantity);
        Assert.Equal(100m, detail.ActualQuantity);
        Assert.Equal(2, await _context.InventoryAudits.CountAsync());
    }

    // UTC-AUDIT-002
    [Fact]
    public async Task CreateAuditAsync_NoWarehouse_ThrowsBadRequest()
    {
        var ex = await Assert.ThrowsAsync<BadRequestException>(() => _service.CreateAuditAsync(CreateRequest(99), 1));
        Assert.Equal("Herbal warehouse was not found.", ex.Message);
        Assert.Empty(await _context.InventoryAudits.ToListAsync());
    }

    // UTC-AUDIT-003
    [Fact]
    public async Task CreateAuditAsync_UnfinishedAudit_ThrowsBadRequest()
    {
        await SeedAsync("Đang kiểm kê");
        var ex = await Assert.ThrowsAsync<BadRequestException>(() => _service.CreateAuditAsync(CreateRequest(), 1));
        Assert.Equal("Hiện đang có đợt kiểm kê KK00008 chưa hoàn tất (Đang kiểm kê), không thể bắt đầu đợt kiểm kê toàn kho mới.", ex.Message);
        Assert.Equal(1, await _context.InventoryAudits.CountAsync());
    }

    // UTC-AUDIT-004
    [Fact]
    public async Task UpdateAuditStatusAsync_ValidStatus_UpdatesAudit()
    {
        await SeedAsync("Đề xuất");
        var result = await _service.UpdateAuditStatusAsync(Code, "Chờ kiểm kê");
        Assert.Equal("Chờ kiểm kê", result.Status);
        _context.ChangeTracker.Clear();
        Assert.Equal("Chờ kiểm kê", (await _context.InventoryAudits.SingleAsync()).Status);
    }

    // UTC-AUDIT-005
    [Fact]
    public async Task UpdateAuditStatusAsync_NotFound_ThrowsNotFound()
    {
        var ex = await Assert.ThrowsAsync<NotFoundException>(() => _service.UpdateAuditStatusAsync("KK99999", "Chờ kiểm kê"));
        Assert.Equal("Inventory audit KK99999 was not found.", ex.Message);
    }

    // UTC-AUDIT-006
    [Fact]
    public async Task UpdateAuditStatusAsync_ApprovedAuditCannotCancel()
    {
        await SeedAsync("Đã duyệt");
        var ex = await Assert.ThrowsAsync<BadRequestException>(() => _service.UpdateAuditStatusAsync(Code, "Đã hủy"));
        Assert.Equal("An inventory audit with approved stock adjustments cannot be cancelled.", ex.Message);
    }

    // UTC-AUDIT-007
    [Fact]
    public async Task UpdateAuditCountAsync_SaveDraft_UpdatesQuantity()
    {
        await SeedAsync("Chờ kiểm kê");
        var result = await _service.UpdateAuditCountAsync(Code, CountRequest(95m, false), 1);
        Assert.Equal("Đang kiểm kê", result.Status);
        var detail = Assert.Single(result.Details);
        Assert.Equal(95m, detail.ActualQuantity);
        Assert.Equal(-5m, detail.Discrepancy);
        Assert.Equal(100m, await StockAsync());
        Assert.Equal(95m, (await _context.AuditDetails.SingleAsync()).ActualQuantity);
    }

    // UTC-AUDIT-008
    [Fact]
    public async Task UpdateAuditCountAsync_Complete_SubmitsForApproval()
    {
        await SeedAsync("Đang kiểm kê");
        var result = await _service.UpdateAuditCountAsync(Code, CountRequest(90m, true), 1);
        Assert.Equal("Chờ duyệt", result.Status);
        Assert.Equal(90m, Assert.Single(result.Details).ActualQuantity);
        Assert.Equal(100m, await StockAsync());
    }

    // UTC-AUDIT-009
    [Fact]
    public async Task UpdateAuditCountAsync_OtherStaff_ThrowsForbidden()
    {
        await SeedAsync("Đang kiểm kê");
        var ex = await Assert.ThrowsAsync<ForbiddenException>(() => _service.UpdateAuditCountAsync(Code, CountRequest(95m, false), 2, false));
        Assert.Equal("Phiếu kiểm kê KK00008 được giao cho nhân viên khác thực hiện (User #1). Bạn không có quyền kiểm đếm phiếu này.", ex.Message);
    }

    // UTC-AUDIT-010
    [Fact]
    public async Task UpdateAuditCountAsync_ApprovedAudit_ThrowsBadRequest()
    {
        await SeedAsync("Đã duyệt");
        var ex = await Assert.ThrowsAsync<BadRequestException>(() => _service.UpdateAuditCountAsync(Code, CountRequest(95m, false), 1));
        Assert.Equal("The inventory audit has been approved; quantities cannot be edited.", ex.Message);
    }

    // UTC-AUDIT-011
    [Fact]
    public async Task ApproveAuditAdjustmentAsync_ValidAudit_AdjustsStock()
    {
        await SeedAsync("Chờ duyệt", 90m);
        var result = await _service.ApproveAuditAdjustmentAsync(Code, new ApproveAuditRequest { ManagerOpinion = "Approved" });
        Assert.Equal("Đã duyệt", result.Status);
        Assert.Equal(90m, await StockAsync());
        Assert.Contains("[Quản lý duyệt]: Approved", result.Note);
    }

    // UTC-AUDIT-012
    [Fact]
    public async Task ApproveAuditAdjustmentAsync_Disposal_DeductsDestroyedQuantity()
    {
        await SeedAsync("Chờ duyệt", 90m, reason: "[Tiêu hủy: 10 kg]");
        var result = await _service.ApproveAuditAdjustmentAsync(Code, new ApproveAuditRequest { ManagerOpinion = "Approved with disposal" });
        Assert.Equal("Đã duyệt", result.Status);
        Assert.Equal(80m, await StockAsync());
        Assert.Equal(-20m, Assert.Single(result.Details).Discrepancy);
    }

    // UTC-AUDIT-013
    [Fact]
    public async Task ApproveAuditAdjustmentAsync_CancelledAudit_ThrowsBadRequest()
    {
        await SeedAsync("Đã hủy", 90m);
        var ex = await Assert.ThrowsAsync<BadRequestException>(() => _service.ApproveAuditAdjustmentAsync(Code, new ApproveAuditRequest { ManagerOpinion = "Approved" }));
        Assert.Equal("The inventory audit has been cancelled; stock adjustments cannot be approved.", ex.Message);
        Assert.Equal(100m, await StockAsync());
    }

    // UTC-AUDIT-014
    [Fact]
    public async Task ApproveAuditAdjustmentAsync_AlreadyApproved_DoesNotAdjustAgain()
    {
        await SeedAsync("Đã duyệt", 90m, 80m);
        var result = await _service.ApproveAuditAdjustmentAsync(Code, new ApproveAuditRequest { ManagerOpinion = "Approved again" });
        Assert.Equal("Đã duyệt", result.Status);
        Assert.Equal(80m, await StockAsync());
    }

    public void Dispose() => _context.Dispose();
}

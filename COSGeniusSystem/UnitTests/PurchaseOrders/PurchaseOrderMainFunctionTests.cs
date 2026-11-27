using System;
using System.Linq;
using System.Collections.Generic;
using System.Threading.Tasks;
using COSGeniusSystem.Data;
using COSGeniusSystem.DTOs;
using COSGeniusSystem.Exceptions;
using COSGeniusSystem.Models;
using COSGeniusSystem.Services;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace COSGeniusSystem.UnitTests;

public sealed class PurchaseOrderMainFunctionTests
{
    private static DateOnly Today => DateOnly.FromDateTime(DateTime.Today);
    private sealed class Scope : IDisposable
    {
        internal ApplicationDbContext Db { get; }
        internal PurchaseOrderService Api { get; }
        private Scope()
        {
            Db = new(new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseInMemoryDatabase($"PurchaseOrderMain_{Guid.NewGuid():N}").Options);
            Api = new(Db);
        }
        internal static async Task<Scope> CreateAsync()
        {
            var s = new Scope();
            s.Db.Suppliers.AddRange(
                new Supplier { SupplierId=1,SupplierCode="NCC001",SupplierName="An Phat",IsActive=true },
                new Supplier { SupplierId=2,SupplierCode="NCC002",SupplierName="Minh Tam",IsActive=true });
            s.Db.Herbs.AddRange(
                new Herb { HerbId=1,HerbCode="VT00001",CommonName="Cam thao",Unit="kg",IsActive=true },
                new Herb { HerbId=2,HerbCode="VT00002",CommonName="Do trong",Unit="kg",IsActive=true });
            s.Db.Users.Add(new User { UserId=1,Username="manager",FullName="Manager",PasswordHash="test_hash",IsActive=true });
            s.Db.Warehouses.Add(new Warehouse { WarehouseId=1,WarehouseCode="K001",WarehouseName="Test warehouse",IsActive=true });
            await s.Db.SaveChangesAsync(); return s;
        }
        internal async Task<PurchaseOrder> OrderAsync(int id=1,string status="Nháp",int supplier=1,int herb=1,DateTime? date=null)
        {
            var order=new PurchaseOrder { Poid=id,Pocode=$"PO{id:D5}",SupplierId=supplier,CreatedBy=1,
                OrderDate=date??DateTime.Now,Status=status,TotalAmount=500000m };
            order.Podetails.Add(new Podetail { HerbId=herb,OrderQuantity=10m,UnitPrice=50000m });
            Db.PurchaseOrders.Add(order); await Db.SaveChangesAsync(); return order;
        }
        internal async Task ReceiptAsync(PurchaseOrder order,int id,string status,decimal quantity)
        {
            var batch=new Batch { BatchId=id,HerbId=1,WarehouseId=1,BatchNumber=$"B{id}",
                ImportDate=DateTime.Now,ExpiryDate=Today.AddYears(1),OriginalQuantity=quantity,
                CurrentQuantity=quantity,UnitCost=50000m,Status="Đạt chuẩn" };
            var receipt=new GoodsReceipt { ReceiptId=id,ReceiptCode=$"GR{id}",Po=order,Poid=order.Poid,
                WarehouseId=1,ReceivedBy=1,ReceiptDate=DateTime.Now,Status=status,TotalCost=quantity*50000m };
            receipt.ReceiptDetails.Add(new ReceiptDetail { Batch=batch,BatchId=id,Quantity=quantity,UnitPrice=50000m });
            Db.GoodsReceipts.Add(receipt); await Db.SaveChangesAsync();
        }
        // Compare persisted values after rejection, not still-tracked entities.
        // InMemory cannot prove SQL rollback, database constraints or locks.
        internal async Task<string> OrdersSnapshotAsync()
        {
            Db.ChangeTracker.Clear();
            var purchaseOrders = await Db.PurchaseOrders.AsNoTracking().OrderBy(p => p.Poid)
                .Select(p => new
                {
                    p.Poid, p.Pocode, p.SupplierId, p.CreatedBy, p.RequestId, p.OrderDate,
                    p.DeliveryDate, p.TotalAmount, p.Status, p.DriverName, p.VehicleNumber
                }).ToListAsync();
            var purchaseLines = await Db.Podetails.AsNoTracking().OrderBy(d => d.Poid).ThenBy(d => d.HerbId)
                .Select(d => new { d.Poid, d.HerbId, d.OrderQuantity, d.UnitPrice }).ToListAsync();
            var receipts = await Db.GoodsReceipts.AsNoTracking().OrderBy(g => g.ReceiptId)
                .Select(g => new
                {
                    g.ReceiptId, g.ReceiptCode, g.Poid, g.WarehouseId, g.ReceivedBy,
                    g.ReceiptDate, g.TotalCost, g.Status, g.Note, g.DriverName,
                    g.VehicleNumber, g.DocumentUrl
                }).ToListAsync();
            var receiptLines = await Db.ReceiptDetails.AsNoTracking().OrderBy(d => d.ReceiptDetailId)
                .Select(d => new { d.ReceiptDetailId, d.ReceiptId, d.BatchId, d.Quantity, d.UnitPrice }).ToListAsync();
            var batches = await Db.Batches.AsNoTracking().OrderBy(b => b.BatchId)
                .Select(b => new
                {
                    b.BatchId, b.HerbId, b.WarehouseId, b.BatchNumber, b.OriginalQuantity,
                    b.CurrentQuantity, b.UnitCost, b.Status, b.StorageLocationId,
                    b.Location, b.ExpiryDate, b.SupplierBatchCode
                }).ToListAsync();
            var history = await Db.ReceiptHistories.AsNoTracking().OrderBy(h => h.ReceiptId).ThenBy(h => h.BatchId)
                .Select(h => new { h.ReceiptId, h.BatchId, h.QuantityChanged, h.BalanceAfter, h.ChangeDate }).ToListAsync();
            return System.Text.Json.JsonSerializer.Serialize(new
            {
                purchaseOrders, purchaseLines, receipts, receiptLines, batches, history,
                NotificationCount = await Db.Notifications.CountAsync()
            });
        }

        public void Dispose()=>Db.Dispose();
    }
    private static CreatePORequest CreateRequest()=>new() { SupplierId=1,DeliveryDate=Today.AddDays(7),Status="Nháp",
        Items=new List<CreatePOItemRequest> { new(){HerbId=1,OrderQuantity=10m,UnitPrice=50000m} } };
    private static UpdatePORequest UpdateRequest()=>new() { SupplierId=1,DeliveryDate=Today.AddDays(7),
        Items=new List<CreatePOItemRequest> { new(){HerbId=1,OrderQuantity=20m,UnitPrice=50000m} } };

    #region CreatePOAsync

    // UTC-PO-MAIN-001: Tested method = CreatePOAsync
    [Fact]
    [Trait("Module", "PO")]
    [Trait("Method", "CreatePOAsync")]
    [Trait("TestCaseId", "UTC-PO-MAIN-001")]
    public async Task CreatePOAsync_ValidRequestCalculatesAndPersistsOrder()
    {
        using var s=await Scope.CreateAsync();var request=CreateRequest();
        request.Items.Add(new(){HerbId=2,OrderQuantity=5m,UnitPrice=100000m});
        request.DriverName=" Driver ";request.VehicleNumber=" Vehicle ";
        var result=await s.Api.CreatePOAsync(1,request);Assert.Equal("PO00001",result.Pocode);
        Assert.Equal("Nháp",result.Status);Assert.Equal(1000000m,result.TotalAmount);Assert.Equal(2,result.Details.Count);
        s.Db.ChangeTracker.Clear();var saved=await s.Db.PurchaseOrders.Include(x=>x.Podetails).SingleAsync();
        Assert.Equal(1,saved.CreatedBy);Assert.Equal("Driver",saved.DriverName);Assert.Equal("Vehicle",saved.VehicleNumber);
        Assert.Equal(1000000m,saved.TotalAmount);Assert.Equal(2,saved.Podetails.Count);
    }

    // UTC-PO-MAIN-002: Tested method = CreatePOAsync
    [Fact]
    [Trait("Module", "PO")]
    [Trait("Method", "CreatePOAsync")]
    [Trait("TestCaseId", "UTC-PO-MAIN-002")]
    public async Task CreatePOAsync_MissingSupplierIsRejected()
    {
        using var s=await Scope.CreateAsync();var request=CreateRequest();request.SupplierId=999;
        var before1 = await s.OrdersSnapshotAsync();
        await Assert.ThrowsAsync<NotFoundException>(()=>s.Api.CreatePOAsync(1,request));
        Assert.Equal(before1, await s.OrdersSnapshotAsync());Assert.False(await s.Db.PurchaseOrders.AnyAsync());
    }

    // UTC-PO-MAIN-003: Tested method = CreatePOAsync
    [Fact]
    [Trait("Module", "PO")]
    [Trait("Method", "CreatePOAsync")]
    [Trait("TestCaseId", "UTC-PO-MAIN-003")]
    public async Task CreatePOAsync_InvalidItemListsAreRejected()
    {
        using var s=await Scope.CreateAsync();
        var invalid=new List<List<CreatePOItemRequest>> {
            new(),new(){new(){HerbId=1,OrderQuantity=0,UnitPrice=50000}},
            new(){new(){HerbId=1,OrderQuantity=-5,UnitPrice=50000}},
            new(){new(){HerbId=1,OrderQuantity=5,UnitPrice=-1000}},
            new(){new(){HerbId=1,OrderQuantity=5,UnitPrice=50000},new(){HerbId=1,OrderQuantity=1,UnitPrice=50000}}
        };
        foreach(var items in invalid) { var request=CreateRequest();request.Items=items;
            var before1 = await s.OrdersSnapshotAsync();
        await Assert.ThrowsAsync<BadRequestException>(()=>s.Api.CreatePOAsync(1,request));
        Assert.Equal(before1, await s.OrdersSnapshotAsync()); }
        Assert.False(await s.Db.PurchaseOrders.AnyAsync());Assert.False(await s.Db.Podetails.AnyAsync());
    }

    // UTC-PO-MAIN-004: Tested method = CreatePOAsync
    [Fact]
    [Trait("Module", "PO")]
    [Trait("Method", "CreatePOAsync")]
    [Trait("TestCaseId", "UTC-PO-MAIN-004")]
    public async Task CreatePOAsync_MissingHerbIsRejected()
    {
        using var s=await Scope.CreateAsync();var request=CreateRequest();request.Items[0].HerbId=999;
        var before1 = await s.OrdersSnapshotAsync();
        await Assert.ThrowsAsync<NotFoundException>(()=>s.Api.CreatePOAsync(1,request));
        Assert.Equal(before1, await s.OrdersSnapshotAsync());Assert.False(await s.Db.PurchaseOrders.AnyAsync());
    }

    // UTC-PO-MAIN-005: Tested method = CreatePOAsync
    [Fact]
    [Trait("Module", "PO")]
    [Trait("Method", "CreatePOAsync")]
    [Trait("TestCaseId", "UTC-PO-MAIN-005")]
    public async Task CreatePOAsync_PastDeliveryDateOrUnsupportedStatusIsRejected()
    {
        using var s=await Scope.CreateAsync();var request=CreateRequest();request.DeliveryDate=Today.AddDays(-1);
        var before1 = await s.OrdersSnapshotAsync();
        await Assert.ThrowsAsync<BadRequestException>(()=>s.Api.CreatePOAsync(1,request));
        Assert.Equal(before1, await s.OrdersSnapshotAsync());
        request=CreateRequest();request.Status="UNKNOWN";
        var before2 = await s.OrdersSnapshotAsync();
        await Assert.ThrowsAsync<BadRequestException>(()=>s.Api.CreatePOAsync(1,request));
        Assert.Equal(before2, await s.OrdersSnapshotAsync());Assert.False(await s.Db.PurchaseOrders.AnyAsync());
    }

    #endregion

    #region UpdatePOAsync

    // UTC-PO-MAIN-006: Tested method = UpdatePOAsync
    [Fact]
    [Trait("Module", "PO")]
    [Trait("Method", "UpdatePOAsync")]
    [Trait("TestCaseId", "UTC-PO-MAIN-006")]
    public async Task UpdatePOAsync_DraftOrderReplacesLinesAndRecalculatesTotal()
    {
        using var s=await Scope.CreateAsync();await s.OrderAsync();var request=UpdateRequest();request.SupplierId=2;
        request.Items=new(){new(){HerbId=2,OrderQuantity=20m,UnitPrice=50000m}};
        var result=await s.Api.UpdatePOAsync(1,request);Assert.Equal(1000000m,result.TotalAmount);Assert.Equal(2,result.SupplierId);
        s.Db.ChangeTracker.Clear();var saved=await s.Db.PurchaseOrders.Include(x=>x.Podetails).SingleAsync();
        Assert.Equal(1000000m,saved.TotalAmount);var line=Assert.Single(saved.Podetails);
        Assert.Equal(2,line.HerbId);Assert.Equal(20m,line.OrderQuantity);Assert.Equal(1,await s.Db.Podetails.CountAsync());
    }

    // UTC-PO-MAIN-007: Tested method = UpdatePOAsync
    [Fact]
    [Trait("Module", "PO")]
    [Trait("Method", "UpdatePOAsync")]
    [Trait("TestCaseId", "UTC-PO-MAIN-007")]
    public async Task UpdatePOAsync_MissingOrderIsRejected()
    {
        using var s=await Scope.CreateAsync();var before1 = await s.OrdersSnapshotAsync();
        await Assert.ThrowsAsync<NotFoundException>(()=>s.Api.UpdatePOAsync(999,UpdateRequest()));
        Assert.Equal(before1, await s.OrdersSnapshotAsync());
    }

    // UTC-PO-MAIN-008: Tested method = UpdatePOAsync
    [Fact]
    [Trait("Module", "PO")]
    [Trait("Method", "UpdatePOAsync")]
    [Trait("TestCaseId", "UTC-PO-MAIN-008")]
    public async Task UpdatePOAsync_NonDraftOrderIsRejected()
    {
        using var s=await Scope.CreateAsync();await s.OrderAsync(status:"Chờ nhận");
        var before1 = await s.OrdersSnapshotAsync();
        await Assert.ThrowsAsync<BadRequestException>(()=>s.Api.UpdatePOAsync(1,UpdateRequest()));
        Assert.Equal(before1, await s.OrdersSnapshotAsync());
        s.Db.ChangeTracker.Clear();Assert.Equal(500000m,(await s.Db.PurchaseOrders.SingleAsync()).TotalAmount);
    }

    // UTC-PO-MAIN-009: Tested method = UpdatePOAsync
    [Fact]
    [Trait("Module", "PO")]
    [Trait("Method", "UpdatePOAsync")]
    [Trait("TestCaseId", "UTC-PO-MAIN-009")]
    public async Task UpdatePOAsync_EmptyItemsAreRejectedWithoutSavingChanges()
    {
        using var s=await Scope.CreateAsync();await s.OrderAsync();var request=UpdateRequest();request.Items.Clear();
        var before1 = await s.OrdersSnapshotAsync();
        await Assert.ThrowsAsync<BadRequestException>(()=>s.Api.UpdatePOAsync(1,request));
        Assert.Equal(before1, await s.OrdersSnapshotAsync());
        s.Db.ChangeTracker.Clear();Assert.Equal(500000m,(await s.Db.PurchaseOrders.SingleAsync()).TotalAmount);
        Assert.Single(await s.Db.Podetails.ToListAsync());
    }

    // UTC-PO-MAIN-010: Tested method = UpdatePOAsync
    [Fact]
    [Trait("Module", "PO")]
    [Trait("Method", "UpdatePOAsync")]
    [Trait("TestCaseId", "UTC-PO-MAIN-010")]
    public async Task UpdatePOAsync_InvalidSupplierDateOrStatusIsRejected()
    {
        using var s=await Scope.CreateAsync();await s.OrderAsync();var request=UpdateRequest();request.SupplierId=999;
        var before1 = await s.OrdersSnapshotAsync();
        await Assert.ThrowsAsync<NotFoundException>(()=>s.Api.UpdatePOAsync(1,request));
        Assert.Equal(before1, await s.OrdersSnapshotAsync());
        request=UpdateRequest();request.DeliveryDate=Today.AddDays(-1);
        var before2 = await s.OrdersSnapshotAsync();
        await Assert.ThrowsAsync<BadRequestException>(()=>s.Api.UpdatePOAsync(1,request));
        Assert.Equal(before2, await s.OrdersSnapshotAsync());
        request=UpdateRequest();request.Status="UNKNOWN";
        var before3 = await s.OrdersSnapshotAsync();
        await Assert.ThrowsAsync<BadRequestException>(()=>s.Api.UpdatePOAsync(1,request));
        Assert.Equal(before3, await s.OrdersSnapshotAsync());
        s.Db.ChangeTracker.Clear();Assert.Equal(500000m,(await s.Db.PurchaseOrders.SingleAsync()).TotalAmount);
        Assert.Equal(1,await s.Db.Podetails.CountAsync());
    }

    #endregion

    #region DeletePOAsync

    // UTC-PO-MAIN-011: Tested method = DeletePOAsync
    [Fact]
    [Trait("Module", "PO")]
    [Trait("Method", "DeletePOAsync")]
    [Trait("TestCaseId", "UTC-PO-MAIN-011")]
    public async Task DeletePOAsync_DraftOrderAndItsLinesAreDeleted()
    {
        using var s=await Scope.CreateAsync();await s.OrderAsync();await s.Api.DeletePOAsync(1);
        s.Db.ChangeTracker.Clear();Assert.False(await s.Db.PurchaseOrders.AnyAsync());Assert.False(await s.Db.Podetails.AnyAsync());
    }

    // UTC-PO-MAIN-012: Tested method = DeletePOAsync
    [Fact]
    [Trait("Module", "PO")]
    [Trait("Method", "DeletePOAsync")]
    [Trait("TestCaseId", "UTC-PO-MAIN-012")]
    public async Task DeletePOAsync_MissingOrderIsRejected()
    {
        using var s=await Scope.CreateAsync();var before1 = await s.OrdersSnapshotAsync();
        await Assert.ThrowsAsync<NotFoundException>(()=>s.Api.DeletePOAsync(999));
        Assert.Equal(before1, await s.OrdersSnapshotAsync());
    }

    // UTC-PO-MAIN-013: Tested method = DeletePOAsync
    [Fact]
    [Trait("Module", "PO")]
    [Trait("Method", "DeletePOAsync")]
    [Trait("TestCaseId", "UTC-PO-MAIN-013")]
    public async Task DeletePOAsync_NonDraftOrderIsRejected()
    {
        using var s=await Scope.CreateAsync();await s.OrderAsync(status:"Hoàn thành");
        var before1 = await s.OrdersSnapshotAsync();
        await Assert.ThrowsAsync<BadRequestException>(()=>s.Api.DeletePOAsync(1));
        Assert.Equal(before1, await s.OrdersSnapshotAsync());
        s.Db.ChangeTracker.Clear();Assert.Single(await s.Db.PurchaseOrders.ToListAsync());Assert.Single(await s.Db.Podetails.ToListAsync());
    }

    // UTC-PO-MAIN-014: Tested method = DeletePOAsync
    [Fact]
    [Trait("Module", "PO")]
    [Trait("Method", "DeletePOAsync")]
    [Trait("TestCaseId", "UTC-PO-MAIN-014")]
    public async Task DeletePOAsync_SupportedDraftAliasesCanBeDeleted()
    {
        foreach(var status in new[]{"DRAFT","Mới tạo"}) { using var s=await Scope.CreateAsync();await s.OrderAsync(status:status);
            await s.Api.DeletePOAsync(1);s.Db.ChangeTracker.Clear();Assert.False(await s.Db.PurchaseOrders.AnyAsync()); }
    }

    // UTC-PO-MAIN-015: Tested method = DeletePOAsync
    [Fact]
    [Trait("Module", "PO")]
    [Trait("Method", "DeletePOAsync")]
    [Trait("TestCaseId", "UTC-PO-MAIN-015")]
    public async Task DeletePOAsync_OtherOrdersAndLinesRemainUnchanged()
    {
        using var s=await Scope.CreateAsync();await s.OrderAsync();await s.OrderAsync(2,herb:2);
        await s.Api.DeletePOAsync(1);s.Db.ChangeTracker.Clear();
        Assert.Equal(2,(await s.Db.PurchaseOrders.SingleAsync()).Poid);
        Assert.Equal(2,(await s.Db.Podetails.SingleAsync()).Poid);
    }

    #endregion
}

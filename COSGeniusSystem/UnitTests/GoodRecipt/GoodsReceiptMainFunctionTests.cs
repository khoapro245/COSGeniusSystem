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
using Microsoft.EntityFrameworkCore.Diagnostics;
using Xunit;

namespace COSGeniusSystem.UnitTests;

public sealed class GoodsReceiptMainFunctionTests
{
    private static DateOnly Today => DateOnly.FromDateTime(DateTime.Today);
    private static CreateGoodsReceiptRequest Request() => new() {
        Poid=1,WarehouseId=1,Note="Receipt note",DriverName="Driver",VehicleNumber="Vehicle",DocumentUrl="/test/document.pdf",
        Items = new() { new() { HerbId=1,SupplierBatchCode="SUP-001",ExpiryDate=Today.AddYears(1),
            Quantity=5m,UnitPrice=50000m,StorageLocationId=1,Location="Shelf A" } }
    };
    private sealed class Scope : IDisposable
    {
        internal ApplicationDbContext Db { get; }
        internal GoodsReceiptService Api { get; }
        private Scope()
        {
            Db=new(new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseInMemoryDatabase($"GoodsReceiptMain_{Guid.NewGuid():N}")
                // InMemory has no transaction semantics. SQL rollback/locks are NOT tested.
                .ConfigureWarnings(w=>w.Ignore(InMemoryEventId.TransactionIgnoredWarning)).Options);
            Api=new(Db);
        }
        internal static async Task<Scope> CreateAsync()
        {
            var s=new Scope();
            var staff=new User { UserId=1,Username="inventory",FullName="Inventory staff",PasswordHash="hash",IsActive=true };
            var manager=new User { UserId=2,Username="manager",FullName="Manager",PasswordHash="hash",IsActive=true };
            manager.UserRoles.Add(new UserRole { Role=new Role { RoleId=1,RoleCode="MANAGER",RoleName="Manager" } });
            s.Db.Users.AddRange(staff,manager);
            s.Db.Suppliers.Add(new Supplier { SupplierId=1,SupplierCode="NCC001",SupplierName="Supplier",IsActive=true });
            s.Db.Herbs.AddRange(
                new Herb { HerbId=1,HerbCode="H1",CommonName="Herb 1",Unit="kg",IsActive=true },
                new Herb { HerbId=2,HerbCode="H2",CommonName="Herb 2",Unit="kg",IsActive=true });
            s.Db.Warehouses.AddRange(
                new Warehouse { WarehouseId=1,WarehouseCode="K1",WarehouseName="Warehouse 1",Location="Warehouse room",IsActive=true },
                new Warehouse { WarehouseId=2,WarehouseCode="K2",WarehouseName="Warehouse 2",IsActive=true });
            s.Db.WarehouseStorageLocations.AddRange(
                new WarehouseStorageLocation { Id=1,WarehouseId=1,Code="S1",StorageType="STANDARD",MaxCapacityGrams=10000m,IsActive=true },
                new WarehouseStorageLocation { Id=2,WarehouseId=1,Code="C1",StorageType="COLD",MaxCapacityGrams=10000m,IsActive=true },
                new WarehouseStorageLocation { Id=3,WarehouseId=2,Code="S2",StorageType="STANDARD",MaxCapacityGrams=10000m,IsActive=true },
                new WarehouseStorageLocation { Id=4,WarehouseId=1,Code="OFF",StorageType="STANDARD",MaxCapacityGrams=10000m,IsActive=false });
            for(var id=1;id<=2;id++) {
                var po=new PurchaseOrder { Poid=id,Pocode=$"PO{id:D5}",SupplierId=1,CreatedBy=2,
                    Status="Chờ nhận",OrderDate=DateTime.Now,TotalAmount=500000m };
                po.Podetails.Add(new Podetail { HerbId=1,OrderQuantity=10m,UnitPrice=50000m });
                s.Db.PurchaseOrders.Add(po);
            }
            await s.Db.SaveChangesAsync();return s;
        }
        internal async Task<Batch> BatchAsync(int id=1,decimal current=0m,decimal original=5m)
        {
            var batch=new Batch { BatchId=id,HerbId=1,WarehouseId=1,StorageLocationId=1,BatchNumber=$"TEST-B{id}",
                SupplierBatchCode=$"SUP-{id}",ImportDate=DateTime.Now,ExpiryDate=Today.AddYears(1),
                OriginalQuantity=original,CurrentQuantity=current,UnitCost=50000m,
                Status=current>0?"Đạt chuẩn":"Chờ duyệt",Location="Shelf A" };
            Db.Batches.Add(batch);await Db.SaveChangesAsync();return batch;
        }
        internal async Task<GoodsReceipt> ReceiptAsync(int id=1,string status=GoodsReceiptStatusConstants.PendingApproval,
            int poId=1,decimal quantity=5m,decimal current=0m,DateTime? date=null)
        {
            var batch=await BatchAsync(id,current,quantity);
            var receipt=new GoodsReceipt { ReceiptId=id,ReceiptCode=$"PN{id:D5}",Poid=poId,WarehouseId=1,
                ReceivedBy=1,ReceiptDate=date??DateTime.Now,Status=status,TotalCost=quantity*50000m,
                Note="Original note",DriverName="Driver",VehicleNumber="Vehicle",DocumentUrl="/test/doc.pdf" };
            receipt.ReceiptDetails.Add(new ReceiptDetail { Batch=batch,BatchId=id,Quantity=quantity,UnitPrice=50000m });
            Db.GoodsReceipts.Add(receipt);await Db.SaveChangesAsync();return receipt;
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

    #region CreateGoodsReceiptAsync

    // UTC-GR-MAIN-001: Tested method = CreateGoodsReceiptAsync
    [Fact]
    [Trait("Module", "GR")]
    [Trait("Method", "CreateGoodsReceiptAsync")]
    [Trait("TestCaseId", "UTC-GR-MAIN-001")]
    public async Task CreateGoodsReceiptAsync_CreatesPendingReceiptWithoutIncreasingStockAndUsesUniqueCodes()
    {
        using var s=await Scope.CreateAsync();var first=await s.Api.CreateGoodsReceiptAsync(1,Request());
        var second=await s.Api.CreateGoodsReceiptAsync(1,Request());
        Assert.Equal("PN00001",first.ReceiptCode);Assert.Equal("PN00002",second.ReceiptCode);
        Assert.Equal(GoodsReceiptStatusConstants.PendingApproval,first.Status);Assert.Equal(1,first.Poid);Assert.Equal(250000m,first.TotalCost);
        s.Db.ChangeTracker.Clear();var batches=await s.Db.Batches.ToListAsync();Assert.Equal(2,batches.Count);
        Assert.All(batches,b=>{Assert.Equal(0m,b.CurrentQuantity);Assert.Equal(5m,b.OriginalQuantity);});
        Assert.Equal(2,batches.Select(b=>b.BatchNumber).Distinct().Count());Assert.False(await s.Db.ReceiptHistories.AnyAsync());
        Assert.True(await s.Db.Notifications.AnyAsync(n=>n.UserId==2));
    }

    // UTC-GR-MAIN-002: Tested method = CreateGoodsReceiptAsync
    [Fact]
    [Trait("Module", "GR")]
    [Trait("Method", "CreateGoodsReceiptAsync")]
    [Trait("TestCaseId", "UTC-GR-MAIN-002")]
    public async Task CreateGoodsReceiptAsync_AllowsAdditionalHerbOutsideLinkedPurchaseOrder()
    {
        using var s=await Scope.CreateAsync();var request=Request();request.Items.Add(new() {
            HerbId=2,SupplierBatchCode="EXTRA",ExpiryDate=Today.AddYears(1),Quantity=2m,UnitPrice=100000m,StorageLocationId=1 });
        var result=await s.Api.CreateGoodsReceiptAsync(1,request);Assert.Equal(1,result.Poid);Assert.Equal(2,result.Details.Count);
        Assert.Contains(result.Details,x=>x.HerbId==2);Assert.Equal(450000m,result.TotalCost);
        s.Db.ChangeTracker.Clear();Assert.All(await s.Db.Batches.ToListAsync(),b=>Assert.Equal(0m,b.CurrentQuantity));
    }

    // UTC-GR-MAIN-003: Tested method = CreateGoodsReceiptAsync
    [Fact]
    [Trait("Module", "GR")]
    [Trait("Method", "CreateGoodsReceiptAsync")]
    [Trait("TestCaseId", "UTC-GR-MAIN-003")]
    public async Task CreateGoodsReceiptAsync_InvalidLinesAreRejectedWithoutCreatingReceipt()
    {
        using var s=await Scope.CreateAsync();
        foreach(var variant in new[]{"empty","batch","quantity","expiry","price"}) {
            var request=Request();
            if(variant=="empty")request.Items.Clear();
            if(variant=="batch")request.Items[0].SupplierBatchCode="";
            if(variant=="quantity")request.Items[0].Quantity=0;
            if(variant=="expiry")request.Items[0].ExpiryDate=Today;
            if(variant=="price")request.Items[0].UnitPrice=-1m;
            var before1 = await s.OrdersSnapshotAsync();
        await Assert.ThrowsAsync<BadRequestException>(()=>s.Api.CreateGoodsReceiptAsync(1,request));
        Assert.Equal(before1, await s.OrdersSnapshotAsync());
        }
        Assert.False(await s.Db.GoodsReceipts.AnyAsync());Assert.False(await s.Db.Batches.AnyAsync());
    }

    // UTC-GR-MAIN-004: Tested method = CreateGoodsReceiptAsync
    [Fact]
    [Trait("Module", "GR")]
    [Trait("Method", "CreateGoodsReceiptAsync")]
    [Trait("TestCaseId", "UTC-GR-MAIN-004")]
    public async Task CreateGoodsReceiptAsync_DraftOrMissingPurchaseOrderIsRejected()
    {
        using var s=await Scope.CreateAsync();var po=await s.Db.PurchaseOrders.SingleAsync(x=>x.Poid==1);po.Status="Nháp";
        await s.Db.SaveChangesAsync();var before1 = await s.OrdersSnapshotAsync();
        await Assert.ThrowsAsync<BadRequestException>(()=>s.Api.CreateGoodsReceiptAsync(1,Request()));
        Assert.Equal(before1, await s.OrdersSnapshotAsync());
        var request=Request();request.Poid=999;
        var before2 = await s.OrdersSnapshotAsync();
        await Assert.ThrowsAsync<NotFoundException>(()=>s.Api.CreateGoodsReceiptAsync(1,request));
        Assert.Equal(before2, await s.OrdersSnapshotAsync());
        Assert.False(await s.Db.GoodsReceipts.AnyAsync());
    }

    // UTC-GR-MAIN-005: Tested method = CreateGoodsReceiptAsync
    [Fact]
    [Trait("Module", "GR")]
    [Trait("Method", "CreateGoodsReceiptAsync")]
    [Trait("TestCaseId", "UTC-GR-MAIN-005")]
    [Trait("Category", "BusinessRuleRegression")]
    [Trait("CandidateIssue", "GR-CAND-001")]
    [Trait("Defect", "DEF-GR-001")]
    public async Task CreateGoodsReceiptAsync_MissingPurchaseOrderMustBeRejected()
    {
        using var s=await Scope.CreateAsync();var request=Request();request.Poid=null;
        var before1 = await s.OrdersSnapshotAsync();
        await Assert.ThrowsAsync<BadRequestException>(()=>s.Api.CreateGoodsReceiptAsync(1,request));
        Assert.Equal(before1, await s.OrdersSnapshotAsync());
        Assert.False(await s.Db.GoodsReceipts.AnyAsync());Assert.False(await s.Db.Batches.AnyAsync());
    }

    #endregion

    #region SubmitForApprovalAsync

    // UTC-GR-MAIN-006: Tested method = SubmitForApprovalAsync
    [Fact]
    [Trait("Module", "GR")]
    [Trait("Method", "SubmitForApprovalAsync")]
    [Trait("TestCaseId", "UTC-GR-MAIN-006")]
    public async Task SubmitForApprovalAsync_RejectedReceiptIsResubmittedUsingSameId()
    {
        using var s=await Scope.CreateAsync();await s.ReceiptAsync(status:GoodsReceiptStatusConstants.Rejected);
        var result=await s.Api.SubmitForApprovalAsync(1);Assert.Equal(1,result.ReceiptId);Assert.Equal(GoodsReceiptStatusConstants.PendingApproval,result.Status);
        s.Db.ChangeTracker.Clear();Assert.Equal(1,await s.Db.GoodsReceipts.CountAsync());Assert.Equal(0m,(await s.Db.Batches.SingleAsync()).CurrentQuantity);
    }

    // UTC-GR-MAIN-007: Tested method = SubmitForApprovalAsync
    [Fact]
    [Trait("Module", "GR")]
    [Trait("Method", "SubmitForApprovalAsync")]
    [Trait("TestCaseId", "UTC-GR-MAIN-007")]
    public async Task SubmitForApprovalAsync_MissingReceiptIsRejected()
    {
        using var s=await Scope.CreateAsync();var before1 = await s.OrdersSnapshotAsync();
        await Assert.ThrowsAsync<NotFoundException>(()=>s.Api.SubmitForApprovalAsync(999));
        Assert.Equal(before1, await s.OrdersSnapshotAsync());
    }

    // UTC-GR-MAIN-008: Tested method = SubmitForApprovalAsync
    [Fact]
    [Trait("Module", "GR")]
    [Trait("Method", "SubmitForApprovalAsync")]
    [Trait("TestCaseId", "UTC-GR-MAIN-008")]
    public async Task SubmitForApprovalAsync_ResubmissionPreservesReceiptDetailsAndNotes()
    {
        using var s=await Scope.CreateAsync();var receipt=await s.ReceiptAsync(status:GoodsReceiptStatusConstants.Rejected);
        var originalLine=receipt.ReceiptDetails.Single().ReceiptDetailId;
        await s.Api.SubmitForApprovalAsync(1);s.Db.ChangeTracker.Clear();
        var saved=await s.Db.GoodsReceipts.Include(x=>x.ReceiptDetails).SingleAsync();
        Assert.Equal("Original note",saved.Note);Assert.Equal(originalLine,Assert.Single(saved.ReceiptDetails).ReceiptDetailId);
        Assert.Equal(5m,saved.ReceiptDetails.Single().Quantity);Assert.False(await s.Db.ReceiptHistories.AnyAsync());
    }

    // UTC-GR-MAIN-009: Tested method = SubmitForApprovalAsync
    [Fact]
    [Trait("Module", "GR")]
    [Trait("Method", "SubmitForApprovalAsync")]
    [Trait("TestCaseId", "UTC-GR-MAIN-009")]
    public async Task SubmitForApprovalAsync_PendingRetryDoesNotDuplicateReceiptOrStock()
    {
        using var s=await Scope.CreateAsync();await s.ReceiptAsync();
        await s.Api.SubmitForApprovalAsync(1);await s.Api.SubmitForApprovalAsync(1);s.Db.ChangeTracker.Clear();
        Assert.Equal(1,await s.Db.GoodsReceipts.CountAsync());Assert.Equal(1,await s.Db.ReceiptDetails.CountAsync());
        Assert.Equal(0m,(await s.Db.Batches.SingleAsync()).CurrentQuantity);Assert.False(await s.Db.ReceiptHistories.AnyAsync());
    }

    // UTC-GR-MAIN-010: Tested method = SubmitForApprovalAsync
    [Fact]
    [Trait("Module", "GR")]
    [Trait("Method", "SubmitForApprovalAsync")]
    [Trait("TestCaseId", "UTC-GR-MAIN-010")]
    [Trait("Category", "BusinessRuleRegression")]
    [Trait("CandidateIssue", "GR-CAND-002")]
    [Trait("Defect", "DEF-GR-002")]
    public async Task SubmitForApprovalAsync_ApprovedReceiptMustNotReturnToPending()
    {
        using var s=await Scope.CreateAsync();await s.ReceiptAsync(status:GoodsReceiptStatusConstants.Approved,current:5m);
        var before1 = await s.OrdersSnapshotAsync();
        await Assert.ThrowsAsync<BadRequestException>(()=>s.Api.SubmitForApprovalAsync(1));
        Assert.Equal(before1, await s.OrdersSnapshotAsync());
        s.Db.ChangeTracker.Clear();Assert.Equal(GoodsReceiptStatusConstants.Approved,(await s.Db.GoodsReceipts.SingleAsync()).Status);
        Assert.Equal(5m,(await s.Db.Batches.SingleAsync()).CurrentQuantity);
    }

    #endregion

    #region ApproveGoodsReceiptAsync

    // UTC-GR-MAIN-011: Tested method = ApproveGoodsReceiptAsync
    [Fact]
    [Trait("Module", "GR")]
    [Trait("Method", "ApproveGoodsReceiptAsync")]
    [Trait("TestCaseId", "UTC-GR-MAIN-011")]
    public async Task ApproveGoodsReceiptAsync_ApprovalAddsStockAndUpdatesHistoryPriceAndPoProgress()
    {
        using var s=await Scope.CreateAsync();await s.ReceiptAsync(current:2m);
        var request=new ApproveGoodsReceiptRequest { IsApproved=true,ManagerNote="Checked",
            Items=new(){new(){BatchId=1,UnitPrice=60000m,Location=" New shelf ",StorageLocationId=1}} };
        var result=await s.Api.ApproveGoodsReceiptAsync(2,1,request);Assert.Equal(GoodsReceiptStatusConstants.Approved,result.Status);
        s.Db.ChangeTracker.Clear();var batch=await s.Db.Batches.SingleAsync();Assert.Equal(7m,batch.CurrentQuantity);
        Assert.Equal("Đạt chuẩn",batch.Status);Assert.Equal(60000m,batch.UnitCost);Assert.Equal("New shelf",batch.Location);
        Assert.Equal(60000m,(await s.Db.Herbs.SingleAsync(h=>h.HerbId==1)).LastImportPrice);
        var history=await s.Db.ReceiptHistories.SingleAsync();Assert.Equal(5m,history.QuantityChanged);Assert.Equal(7m,history.BalanceAfter);
        Assert.Equal("Nhận 1 phần",(await s.Db.PurchaseOrders.SingleAsync(p=>p.Poid==1)).Status);
        await s.ReceiptAsync(2,quantity:5m);await s.Api.ApproveGoodsReceiptAsync(2,2,new(){IsApproved=true});
        s.Db.ChangeTracker.Clear();Assert.Equal("Hoàn thành",(await s.Db.PurchaseOrders.SingleAsync(p=>p.Poid==1)).Status);
        Assert.Equal(2,await s.Db.ReceiptHistories.CountAsync());
    }

    // UTC-GR-MAIN-012: Tested method = ApproveGoodsReceiptAsync
    [Fact]
    [Trait("Module", "GR")]
    [Trait("Method", "ApproveGoodsReceiptAsync")]
    [Trait("TestCaseId", "UTC-GR-MAIN-012")]
    public async Task ApproveGoodsReceiptAsync_RejectionDoesNotIncreaseStockAndNotifiesCreator()
    {
        using var s=await Scope.CreateAsync();await s.ReceiptAsync();
        var result=await s.Api.ApproveGoodsReceiptAsync(2,1,new(){IsApproved=false,ManagerNote="Damaged"});
        Assert.Equal(GoodsReceiptStatusConstants.Rejected,result.Status);s.Db.ChangeTracker.Clear();
        var batch=await s.Db.Batches.SingleAsync();Assert.Equal(0m,batch.CurrentQuantity);Assert.Equal("Từ chối",batch.Status);
        Assert.False(await s.Db.ReceiptHistories.AnyAsync());Assert.True(await s.Db.Notifications.AnyAsync(n=>n.UserId==1));
        Assert.Contains("Damaged",(await s.Db.GoodsReceipts.SingleAsync()).Note);
        Assert.Equal("Chờ nhận",(await s.Db.PurchaseOrders.SingleAsync(p=>p.Poid==1)).Status);
    }

    // UTC-GR-MAIN-013: Tested method = ApproveGoodsReceiptAsync
    [Fact]
    [Trait("Module", "GR")]
    [Trait("Method", "ApproveGoodsReceiptAsync")]
    [Trait("TestCaseId", "UTC-GR-MAIN-013")]
    public async Task ApproveGoodsReceiptAsync_AlreadyApprovedReceiptCannotBeApprovedAgain()
    {
        using var s=await Scope.CreateAsync();await s.ReceiptAsync(status:GoodsReceiptStatusConstants.Approved,current:5m);
        var before1 = await s.OrdersSnapshotAsync();
        await Assert.ThrowsAsync<BadRequestException>(()=>s.Api.ApproveGoodsReceiptAsync(2,1,new(){IsApproved=true}));
        Assert.Equal(before1, await s.OrdersSnapshotAsync());
        s.Db.ChangeTracker.Clear();Assert.Equal(5m,(await s.Db.Batches.SingleAsync()).CurrentQuantity);
        Assert.False(await s.Db.ReceiptHistories.AnyAsync());
    }

    // UTC-GR-MAIN-014: Tested method = ApproveGoodsReceiptAsync
    [Fact]
    [Trait("Module", "GR")]
    [Trait("Method", "ApproveGoodsReceiptAsync")]
    [Trait("TestCaseId", "UTC-GR-MAIN-014")]
    public async Task ApproveGoodsReceiptAsync_MissingReceiptIsRejected()
    {
        using var s=await Scope.CreateAsync();
        var before1 = await s.OrdersSnapshotAsync();
        await Assert.ThrowsAsync<NotFoundException>(()=>s.Api.ApproveGoodsReceiptAsync(2,999,new(){IsApproved=true}));
        Assert.Equal(before1, await s.OrdersSnapshotAsync());
    }

    // UTC-GR-MAIN-015: Tested method = ApproveGoodsReceiptAsync
    [Fact]
    [Trait("Module", "GR")]
    [Trait("Method", "ApproveGoodsReceiptAsync")]
    [Trait("TestCaseId", "UTC-GR-MAIN-015")]
    [Trait("Category", "BusinessRuleRegression")]
    [Trait("CandidateIssue", "GR-CAND-003")]
    [Trait("Defect", "DEF-GR-003")]
    public async Task ApproveGoodsReceiptAsync_NewBatchOriginalQuantityMustNotBeCountedTwice()
    {
        using var s=await Scope.CreateAsync();var created=await s.Api.CreateGoodsReceiptAsync(1,Request());
        await s.Api.ApproveGoodsReceiptAsync(2,created.ReceiptId,new(){IsApproved=true});s.Db.ChangeTracker.Clear();
        var batch=await s.Db.Batches.SingleAsync();Assert.Equal(5m,batch.CurrentQuantity);Assert.Equal(5m,batch.OriginalQuantity);
    }

    #endregion
}

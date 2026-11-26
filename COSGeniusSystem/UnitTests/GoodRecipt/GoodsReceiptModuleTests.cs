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

public class GoodsReceiptModuleTests
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
                .UseInMemoryDatabase($"GoodsReceiptTests_{Guid.NewGuid():N}")
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
        public void Dispose()=>Db.Dispose();
    }

    // UTC-GR-001: List receipts in descending receipt-date order.
    [Fact]
    public async Task GetGoodsReceiptsAsync_ListsNewestReceiptsFirst()
    {
        using var s=await Scope.CreateAsync();var now=DateTime.Now;
        await s.ReceiptAsync(1,date:now.AddMinutes(-1));await s.ReceiptAsync(2,date:now);
        var result=await s.Api.GetGoodsReceiptsAsync();Assert.Equal(new[]{2,1},result.Select(x=>x.ReceiptId).ToArray());
    }

    // UTC-GR-002: Filter receipts by approved status.
    [Fact]
    public async Task GetGoodsReceiptsAsync_FiltersApprovedStatus()
    {
        using var s=await Scope.CreateAsync();await s.ReceiptAsync();await s.ReceiptAsync(2,GoodsReceiptStatusConstants.Approved);
        Assert.Equal(2,Assert.Single(await s.Api.GetGoodsReceiptsAsync(status:"approved")).ReceiptId);
    }

    // UTC-GR-003: Filter receipts by their linked purchase order.
    [Fact]
    public async Task GetGoodsReceiptsAsync_FiltersLinkedPurchaseOrder()
    {
        using var s=await Scope.CreateAsync();await s.ReceiptAsync();await s.ReceiptAsync(2,poId:2);
        Assert.Equal(2,Assert.Single(await s.Api.GetGoodsReceiptsAsync(poId:2)).ReceiptId);
    }

    // UTC-GR-004: Return an empty list when no goods receipt exists.
    [Fact]
    public async Task GetGoodsReceiptsAsync_NoReceiptsReturnsEmpty()
    {
        using var s=await Scope.CreateAsync();Assert.Empty(await s.Api.GetGoodsReceiptsAsync());
    }

    // UTC-GR-005: Apply status and PO filters together.
    [Fact]
    public async Task GetGoodsReceiptsAsync_CombinesStatusAndPurchaseOrderFilters()
    {
        using var s=await Scope.CreateAsync();await s.ReceiptAsync(1,GoodsReceiptStatusConstants.Approved);
        await s.ReceiptAsync(2,GoodsReceiptStatusConstants.Rejected,poId:2);await s.ReceiptAsync(3,GoodsReceiptStatusConstants.Approved,poId:2);
        Assert.Equal(3,Assert.Single(await s.Api.GetGoodsReceiptsAsync("approved",2)).ReceiptId);
    }

    // UTC-GR-006: Retrieve receipt details by ID, including batch and shelf information.
    [Fact]
    public async Task GetGoodsReceiptDetailAsync_ExistingIdReturnsBatchAndLocationDetails()
    {
        using var s=await Scope.CreateAsync();await s.ReceiptAsync();var result=await s.Api.GetGoodsReceiptDetailAsync("1");
        Assert.NotNull(result);Assert.Equal(1,result.ReceiptId);Assert.Equal(1,result.Poid);
        var line=Assert.Single(result.Details);Assert.Equal(1,line.HerbId);Assert.Equal(1,line.BatchId);
        Assert.Equal("TEST-B1",line.BatchNumber);Assert.Equal("S1",line.StorageLocationCode);Assert.Equal("S1",line.Location);
    }

    // UTC-GR-007: Retrieve a receipt using a case-insensitive code.
    [Fact]
    public async Task GetGoodsReceiptDetailAsync_ExistingCodeIsCaseInsensitive()
    {
        using var s=await Scope.CreateAsync();await s.ReceiptAsync();
        var result=await s.Api.GetGoodsReceiptDetailAsync("pn00001");Assert.NotNull(result);Assert.Equal(1,result.ReceiptId);
    }

    // UTC-GR-008: Return null for a nonexistent receipt.
    [Fact]
    public async Task GetGoodsReceiptDetailAsync_MissingIdReturnsNull()
    {
        using var s=await Scope.CreateAsync();Assert.Null(await s.Api.GetGoodsReceiptDetailAsync("999"));
    }

    // UTC-GR-009: Map pending, approved and rejected status labels in one grouped test.
    [Fact]
    public async Task GetGoodsReceiptDetailAsync_MapsCanonicalStatusLabels()
    {
        using var s=await Scope.CreateAsync();await s.ReceiptAsync();await s.ReceiptAsync(2,GoodsReceiptStatusConstants.Approved);
        await s.ReceiptAsync(3,GoodsReceiptStatusConstants.Rejected);
        Assert.Equal("Chờ quản lý duyệt",(await s.Api.GetGoodsReceiptDetailAsync("1"))!.StatusLabel);
        Assert.Equal("Đã duyệt nhập",(await s.Api.GetGoodsReceiptDetailAsync("2"))!.StatusLabel);
        Assert.Equal("Từ chối duyệt",(await s.Api.GetGoodsReceiptDetailAsync("3"))!.StatusLabel);
    }

    // UTC-GR-010: Map receipt totals and transport/document fields.
    [Fact]
    public async Task GetGoodsReceiptDetailAsync_MapsTotalsAndTransportInformation()
    {
        using var s=await Scope.CreateAsync();await s.ReceiptAsync();var result=await s.Api.GetGoodsReceiptDetailAsync("1");
        Assert.NotNull(result);Assert.Equal(5m,result.TotalQuantity);Assert.Equal(250000m,result.TotalCost);
        Assert.Equal(250000m,Assert.Single(result.Details).LineTotal);Assert.Equal("Driver",result.DriverName);
        Assert.Equal("Vehicle",result.VehicleNumber);Assert.Equal("/test/doc.pdf",result.DocumentUrl);
    }

    // UTC-GR-011: Create unique pending receipts and batches while leaving available stock unchanged.
    [Fact]
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

    // UTC-GR-012: Allow an extra herb outside the PO while retaining the PO link.
    [Fact]
    public async Task CreateGoodsReceiptAsync_AllowsAdditionalHerbOutsideLinkedPurchaseOrder()
    {
        using var s=await Scope.CreateAsync();var request=Request();request.Items.Add(new() {
            HerbId=2,SupplierBatchCode="EXTRA",ExpiryDate=Today.AddYears(1),Quantity=2m,UnitPrice=100000m,StorageLocationId=1 });
        var result=await s.Api.CreateGoodsReceiptAsync(1,request);Assert.Equal(1,result.Poid);Assert.Equal(2,result.Details.Count);
        Assert.Contains(result.Details,x=>x.HerbId==2);Assert.Equal(450000m,result.TotalCost);
        s.Db.ChangeTracker.Clear();Assert.All(await s.Db.Batches.ToListAsync(),b=>Assert.Equal(0m,b.CurrentQuantity));
    }

    // UTC-GR-013: Reject empty items, missing supplier batch code, zero quantity, expiry today and negative price.
    [Fact]
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
            await Assert.ThrowsAsync<BadRequestException>(()=>s.Api.CreateGoodsReceiptAsync(1,request));
        }
        Assert.False(await s.Db.GoodsReceipts.AnyAsync());Assert.False(await s.Db.Batches.AnyAsync());
    }

    // UTC-GR-014: Reject receipts linked to a draft or nonexistent PO.
    [Fact]
    public async Task CreateGoodsReceiptAsync_DraftOrMissingPurchaseOrderIsRejected()
    {
        using var s=await Scope.CreateAsync();var po=await s.Db.PurchaseOrders.SingleAsync(x=>x.Poid==1);po.Status="Nháp";
        await s.Db.SaveChangesAsync();await Assert.ThrowsAsync<BadRequestException>(()=>s.Api.CreateGoodsReceiptAsync(1,Request()));
        var request=Request();request.Poid=999;
        await Assert.ThrowsAsync<NotFoundException>(()=>s.Api.CreateGoodsReceiptAsync(1,request));
        Assert.False(await s.Db.GoodsReceipts.AnyAsync());
    }

    // UTC-GR-015: Require a PO link before creating a goods receipt.
    [Fact]
    [Trait("Category", "BusinessRuleRegression")]
    [Trait("CandidateIssue", "GR-CAND-001")]
    public async Task CreateGoodsReceiptAsync_MissingPurchaseOrderMustBeRejected()
    {
        using var s=await Scope.CreateAsync();var request=Request();request.Poid=null;
        await Assert.ThrowsAsync<BadRequestException>(()=>s.Api.CreateGoodsReceiptAsync(1,request));
        Assert.False(await s.Db.GoodsReceipts.AnyAsync());Assert.False(await s.Db.Batches.AnyAsync());
    }

    // UTC-GR-016: Resubmit a rejected receipt without creating a replacement receipt.
    [Fact]
    public async Task SubmitForApprovalAsync_RejectedReceiptIsResubmittedUsingSameId()
    {
        using var s=await Scope.CreateAsync();await s.ReceiptAsync(status:GoodsReceiptStatusConstants.Rejected);
        var result=await s.Api.SubmitForApprovalAsync(1);Assert.Equal(1,result.ReceiptId);Assert.Equal(GoodsReceiptStatusConstants.PendingApproval,result.Status);
        s.Db.ChangeTracker.Clear();Assert.Equal(1,await s.Db.GoodsReceipts.CountAsync());Assert.Equal(0m,(await s.Db.Batches.SingleAsync()).CurrentQuantity);
    }

    // UTC-GR-017: Reject submission of a nonexistent receipt.
    [Fact]
    public async Task SubmitForApprovalAsync_MissingReceiptIsRejected()
    {
        using var s=await Scope.CreateAsync();await Assert.ThrowsAsync<NotFoundException>(()=>s.Api.SubmitForApprovalAsync(999));
    }

    // UTC-GR-018: Preserve header and line data when resubmitting the same receipt.
    [Fact]
    public async Task SubmitForApprovalAsync_ResubmissionPreservesReceiptDetailsAndNotes()
    {
        using var s=await Scope.CreateAsync();var receipt=await s.ReceiptAsync(status:GoodsReceiptStatusConstants.Rejected);
        var originalLine=receipt.ReceiptDetails.Single().ReceiptDetailId;
        await s.Api.SubmitForApprovalAsync(1);s.Db.ChangeTracker.Clear();
        var saved=await s.Db.GoodsReceipts.Include(x=>x.ReceiptDetails).SingleAsync();
        Assert.Equal("Original note",saved.Note);Assert.Equal(originalLine,Assert.Single(saved.ReceiptDetails).ReceiptDetailId);
        Assert.Equal(5m,saved.ReceiptDetails.Single().Quantity);Assert.False(await s.Db.ReceiptHistories.AnyAsync());
    }

    // UTC-GR-019: Repeat pending submission without duplicating receipts or increasing stock.
    [Fact]
    public async Task SubmitForApprovalAsync_PendingRetryDoesNotDuplicateReceiptOrStock()
    {
        using var s=await Scope.CreateAsync();await s.ReceiptAsync();
        await s.Api.SubmitForApprovalAsync(1);await s.Api.SubmitForApprovalAsync(1);s.Db.ChangeTracker.Clear();
        Assert.Equal(1,await s.Db.GoodsReceipts.CountAsync());Assert.Equal(1,await s.Db.ReceiptDetails.CountAsync());
        Assert.Equal(0m,(await s.Db.Batches.SingleAsync()).CurrentQuantity);Assert.False(await s.Db.ReceiptHistories.AnyAsync());
    }

    // UTC-GR-020: Prevent reopening an approved receipt for another approval.
    [Fact]
    [Trait("Category", "BusinessRuleRegression")]
    [Trait("CandidateIssue", "GR-CAND-002")]
    public async Task SubmitForApprovalAsync_ApprovedReceiptMustNotReturnToPending()
    {
        using var s=await Scope.CreateAsync();await s.ReceiptAsync(status:GoodsReceiptStatusConstants.Approved,current:5m);
        await Assert.ThrowsAsync<BadRequestException>(()=>s.Api.SubmitForApprovalAsync(1));
        s.Db.ChangeTracker.Clear();Assert.Equal(GoodsReceiptStatusConstants.Approved,(await s.Db.GoodsReceipts.SingleAsync()).Status);
        Assert.Equal(5m,(await s.Db.Batches.SingleAsync()).CurrentQuantity);
    }

    // UTC-GR-021: Approve receipts, add stock, record history, update price and advance PO progress.
    [Fact]
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

    // UTC-GR-022: Reject a receipt without posting stock and notify the receiving staff.
    [Fact]
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

    // UTC-GR-023: Block a direct second approval and leave stock unchanged.
    [Fact]
    public async Task ApproveGoodsReceiptAsync_AlreadyApprovedReceiptCannotBeApprovedAgain()
    {
        using var s=await Scope.CreateAsync();await s.ReceiptAsync(status:GoodsReceiptStatusConstants.Approved,current:5m);
        await Assert.ThrowsAsync<BadRequestException>(()=>s.Api.ApproveGoodsReceiptAsync(2,1,new(){IsApproved=true}));
        s.Db.ChangeTracker.Clear();Assert.Equal(5m,(await s.Db.Batches.SingleAsync()).CurrentQuantity);
        Assert.False(await s.Db.ReceiptHistories.AnyAsync());
    }

    // UTC-GR-024: Reject approval of a nonexistent receipt.
    [Fact]
    public async Task ApproveGoodsReceiptAsync_MissingReceiptIsRejected()
    {
        using var s=await Scope.CreateAsync();
        await Assert.ThrowsAsync<NotFoundException>(()=>s.Api.ApproveGoodsReceiptAsync(2,999,new(){IsApproved=true}));
    }

    // UTC-GR-025: Count a new batch received quantity once in both current and original quantity.
    [Fact]
    [Trait("Category", "BusinessRuleRegression")]
    [Trait("CandidateIssue", "GR-CAND-003")]
    public async Task ApproveGoodsReceiptAsync_NewBatchOriginalQuantityMustNotBeCountedTwice()
    {
        using var s=await Scope.CreateAsync();var created=await s.Api.CreateGoodsReceiptAsync(1,Request());
        await s.Api.ApproveGoodsReceiptAsync(2,created.ReceiptId,new(){IsApproved=true});s.Db.ChangeTracker.Clear();
        var batch=await s.Db.Batches.SingleAsync();Assert.Equal(5m,batch.CurrentQuantity);Assert.Equal(5m,batch.OriginalQuantity);
    }

    // UTC-GR-026: Filter suggested locations by warehouse and active status.
    [Fact]
    public async Task GetSuggestedStorageLocationsAsync_ReturnsOnlyActiveLocationsInSelectedWarehouse()
    {
        using var s=await Scope.CreateAsync();var result=await s.Api.GetSuggestedStorageLocationsAsync(1,null,1);
        Assert.Equal(new[]{1,2},result.Select(x=>x.Id).OrderBy(x=>x).ToArray());
        Assert.Equal(3,Assert.Single(await s.Api.GetSuggestedStorageLocationsAsync(1,null,2)).Id);
    }

    // UTC-GR-027: Prefer cold storage for a herb requiring refrigeration.
    [Fact]
    public async Task GetSuggestedStorageLocationsAsync_ColdHerbRequiresColdStorage()
    {
        using var s=await Scope.CreateAsync();(await s.Db.Herbs.SingleAsync(h=>h.HerbId==1)).IsColdStorageRequired=true;
        await s.Db.SaveChangesAsync();var result=await s.Api.GetSuggestedStorageLocationsAsync(1,100m,1);
        Assert.True(result.Single(x=>x.Id==2).IsSuitable);Assert.False(result.Single(x=>x.Id==1).IsSuitable);Assert.Equal(2,result[0].Id);
    }

    // UTC-GR-028: Accept exact remaining capacity and reject a request exceeding it.
    [Fact]
    public async Task GetSuggestedStorageLocationsAsync_CapacityBoundaryUsesRemainingSpace()
    {
        using var s=await Scope.CreateAsync();(await s.Db.Herbs.SingleAsync(h=>h.HerbId==1)).Unit="g";
        await s.Db.SaveChangesAsync();await s.BatchAsync(current:2500m,original:2500m);
        var exact=(await s.Api.GetSuggestedStorageLocationsAsync(1,7500m,1)).Single(x=>x.Id==1);
        Assert.Equal(2500m,exact.CurrentUsedGrams);Assert.Equal(7500m,exact.RemainingCapacityGrams);Assert.True(exact.IsSuitable);
        Assert.False((await s.Api.GetSuggestedStorageLocationsAsync(1,7501m,1)).Single(x=>x.Id==1).IsSuitable);
    }

    // UTC-GR-029: Prioritize suitable storage type and then greater remaining capacity.
    [Fact]
    public async Task GetSuggestedStorageLocationsAsync_SortsSuitableLocationsByRemainingCapacity()
    {
        using var s=await Scope.CreateAsync();(await s.Db.WarehouseStorageLocations.SingleAsync(l=>l.Id==1)).MaxCapacityGrams=1000m;
        s.Db.WarehouseStorageLocations.Add(new(){Id=5,WarehouseId=1,Code="S5",StorageType="STANDARD",MaxCapacityGrams=2000m,IsActive=true});
        await s.Db.SaveChangesAsync();var result=await s.Api.GetSuggestedStorageLocationsAsync(1,500m,1);
        Assert.Equal(new[]{5,1,2},result.Select(x=>x.Id).ToArray());
    }

    // UTC-GR-030: Convert kg stock to grams before checking shelf capacity.
    [Fact]
    [Trait("Category", "BusinessRuleRegression")]
    [Trait("CandidateIssue", "GR-CAND-004")]
    public async Task GetSuggestedStorageLocationsAsync_KilogramStockMustBeConvertedToGrams()
    {
        using var s=await Scope.CreateAsync();await s.BatchAsync(current:1.5m,original:1.5m);
        var shelf=(await s.Api.GetSuggestedStorageLocationsAsync(1,9000m,1)).Single(x=>x.Id==1);
        Assert.Equal(1500m,shelf.CurrentUsedGrams);Assert.Equal(8500m,shelf.RemainingCapacityGrams);Assert.False(shelf.IsSuitable);
    }

}

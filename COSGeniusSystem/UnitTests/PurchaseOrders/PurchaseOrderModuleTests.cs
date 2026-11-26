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

public class PurchaseOrderModuleTests
{
    private static DateOnly Today => DateOnly.FromDateTime(DateTime.Today);
    private sealed class Scope : IDisposable
    {
        internal ApplicationDbContext Db { get; }
        internal PurchaseOrderService Api { get; }
        private Scope()
        {
            Db = new(new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseInMemoryDatabase($"PurchaseOrderTests_{Guid.NewGuid():N}").Options);
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
        public void Dispose()=>Db.Dispose();
    }
    private static CreatePORequest CreateRequest()=>new() { SupplierId=1,DeliveryDate=Today.AddDays(7),Status="Nháp",
        Items=new List<CreatePOItemRequest> { new(){HerbId=1,OrderQuantity=10m,UnitPrice=50000m} } };
    private static UpdatePORequest UpdateRequest()=>new() { SupplierId=1,DeliveryDate=Today.AddDays(7),
        Items=new List<CreatePOItemRequest> { new(){HerbId=1,OrderQuantity=20m,UnitPrice=50000m} } };

    // UTC-PO-001: List all purchase orders and use descending ID to break equal-date ties.
    [Fact]
    public async Task GetPurchaseOrdersAsync_NoFilterReturnsOrdersNewestFirst()
    {
        using var s=await Scope.CreateAsync();var date=DateTime.Now;
        await s.OrderAsync(1,date:date);await s.OrderAsync(2,date:date);
        var result=await s.Api.GetPurchaseOrdersAsync();
        Assert.Equal(new[]{2,1},result.Select(x=>x.Poid).ToArray());
    }

    // UTC-PO-002: Filter orders by pending delivery status.
    [Fact]
    public async Task GetPurchaseOrdersAsync_PendingFilterReturnsOnlyWaitingOrders()
    {
        using var s=await Scope.CreateAsync();await s.OrderAsync(status:"Chờ nhận");await s.OrderAsync(2,status:"Nháp");
        var result=await s.Api.GetPurchaseOrdersAsync(status:"pending");
        Assert.Equal(1,Assert.Single(result).Poid);Assert.Equal("Chờ nhận",result[0].ProgressStatus);
    }

    // UTC-PO-003: Search using a trimmed and case-insensitive purchase-order code.
    [Fact]
    public async Task GetPurchaseOrdersAsync_SearchByCodeReturnsMatchingOrder()
    {
        using var s=await Scope.CreateAsync();await s.OrderAsync();await s.OrderAsync(2);
        Assert.Equal(1,Assert.Single(await s.Api.GetPurchaseOrdersAsync(search:" po00001 ")).Poid);
    }

    // UTC-PO-004: Search by supplier name, herb name and herb code in one grouped test.
    [Fact]
    public async Task GetPurchaseOrdersAsync_SearchSupplierHerbNameAndHerbCode()
    {
        using var s=await Scope.CreateAsync();await s.OrderAsync();await s.OrderAsync(2,supplier:2,herb:2);
        foreach(var keyword in new[]{"An Phat","Cam thao","VT00001"})
            Assert.Equal(1,Assert.Single(await s.Api.GetPurchaseOrdersAsync(search:keyword)).Poid);
    }

    // UTC-PO-005: Return an empty result when no purchase order exists.
    [Fact]
    public async Task GetPurchaseOrdersAsync_NoOrdersReturnsEmptyList()
    {
        using var s=await Scope.CreateAsync();Assert.Empty(await s.Api.GetPurchaseOrdersAsync());
    }

    // UTC-PO-006: Retrieve purchase-order details by ID.
    [Fact]
    public async Task GetPurchaseOrderDetailAsync_ExistingIdReturnsLinesAndTotals()
    {
        using var s=await Scope.CreateAsync();await s.OrderAsync();var result=await s.Api.GetPurchaseOrderDetailAsync("1");
        Assert.NotNull(result);Assert.Equal(1,result.Poid);Assert.Equal("PO00001",result.Pocode);
        Assert.Equal(500000m,result.TotalAmount);var line=Assert.Single(result.Details);
        Assert.Equal(1,line.HerbId);Assert.Equal(10m,line.OrderQuantity);Assert.Equal(500000m,line.LineTotal);
    }

    // UTC-PO-007: Retrieve purchase-order details by code without case sensitivity.
    [Fact]
    public async Task GetPurchaseOrderDetailAsync_ExistingCodeIsCaseInsensitive()
    {
        using var s=await Scope.CreateAsync();await s.OrderAsync();
        var result=await s.Api.GetPurchaseOrderDetailAsync("po00001");Assert.NotNull(result);Assert.Equal(1,result.Poid);
    }

    // UTC-PO-008: Return null for a nonexistent purchase order.
    [Fact]
    public async Task GetPurchaseOrderDetailAsync_MissingIdReturnsNull()
    {
        using var s=await Scope.CreateAsync();Assert.Null(await s.Api.GetPurchaseOrderDetailAsync("999"));
    }

    // UTC-PO-009: Calculate partial progress using approved receipts only.
    [Fact]
    public async Task GetPurchaseOrderDetailAsync_OnlyApprovedReceiptsContributeToProgress()
    {
        using var s=await Scope.CreateAsync();var order=await s.OrderAsync(status:"Chờ nhận");
        await s.ReceiptAsync(order,1,"APPROVED",2m);await s.ReceiptAsync(order,2,"PENDING_APPROVAL",3m);await s.ReceiptAsync(order,3,"REJECTED",4m);
        var result=await s.Api.GetPurchaseOrderDetailAsync("1");Assert.NotNull(result);var line=Assert.Single(result.Details);
        Assert.Equal(2m,line.ReceivedQuantity);Assert.Equal(8m,line.RemainingQuantity);
        Assert.Equal("Nhận một phần",result.ProgressStatus);
    }

    // UTC-PO-010: Show completion when approved receipts fulfill the ordered quantity.
    [Fact]
    public async Task GetPurchaseOrderDetailAsync_AllRequiredQuantityReceivedShowsCompleted()
    {
        using var s=await Scope.CreateAsync();var order=await s.OrderAsync(status:"Chờ nhận");
        await s.ReceiptAsync(order,1,"APPROVED",10m);
        var result=await s.Api.GetPurchaseOrderDetailAsync("1");Assert.NotNull(result);
        Assert.Equal(0m,Assert.Single(result.Details).RemainingQuantity);Assert.Equal("Đã hoàn thành",result.ProgressStatus);
    }

    // UTC-PO-011: Create an order, calculate totals, trim transport fields and persist its details.
    [Fact]
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

    // UTC-PO-012: Reject creating an order for a nonexistent supplier.
    [Fact]
    public async Task CreatePOAsync_MissingSupplierIsRejected()
    {
        using var s=await Scope.CreateAsync();var request=CreateRequest();request.SupplierId=999;
        await Assert.ThrowsAsync<NotFoundException>(()=>s.Api.CreatePOAsync(1,request));Assert.False(await s.Db.PurchaseOrders.AnyAsync());
    }

    // UTC-PO-013: Reject empty items, zero/negative quantity, negative price and duplicate herbs in one grouped test.
    [Fact]
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
            await Assert.ThrowsAsync<BadRequestException>(()=>s.Api.CreatePOAsync(1,request)); }
        Assert.False(await s.Db.PurchaseOrders.AnyAsync());Assert.False(await s.Db.Podetails.AnyAsync());
    }

    // UTC-PO-014: Reject creating an order containing a nonexistent herb.
    [Fact]
    public async Task CreatePOAsync_MissingHerbIsRejected()
    {
        using var s=await Scope.CreateAsync();var request=CreateRequest();request.Items[0].HerbId=999;
        await Assert.ThrowsAsync<NotFoundException>(()=>s.Api.CreatePOAsync(1,request));Assert.False(await s.Db.PurchaseOrders.AnyAsync());
    }

    // UTC-PO-015: Reject a past delivery date or unsupported creation status.
    [Fact]
    public async Task CreatePOAsync_PastDeliveryDateOrUnsupportedStatusIsRejected()
    {
        using var s=await Scope.CreateAsync();var request=CreateRequest();request.DeliveryDate=Today.AddDays(-1);
        await Assert.ThrowsAsync<BadRequestException>(()=>s.Api.CreatePOAsync(1,request));
        request=CreateRequest();request.Status="UNKNOWN";
        await Assert.ThrowsAsync<BadRequestException>(()=>s.Api.CreatePOAsync(1,request));Assert.False(await s.Db.PurchaseOrders.AnyAsync());
    }

    // UTC-PO-016: Replace draft-order lines, change supplier and recalculate the total.
    [Fact]
    public async Task UpdatePOAsync_DraftOrderReplacesLinesAndRecalculatesTotal()
    {
        using var s=await Scope.CreateAsync();await s.OrderAsync();var request=UpdateRequest();request.SupplierId=2;
        request.Items=new(){new(){HerbId=2,OrderQuantity=20m,UnitPrice=50000m}};
        var result=await s.Api.UpdatePOAsync(1,request);Assert.Equal(1000000m,result.TotalAmount);Assert.Equal(2,result.SupplierId);
        s.Db.ChangeTracker.Clear();var saved=await s.Db.PurchaseOrders.Include(x=>x.Podetails).SingleAsync();
        Assert.Equal(1000000m,saved.TotalAmount);var line=Assert.Single(saved.Podetails);
        Assert.Equal(2,line.HerbId);Assert.Equal(20m,line.OrderQuantity);Assert.Equal(1,await s.Db.Podetails.CountAsync());
    }

    // UTC-PO-017: Reject updating a nonexistent purchase order.
    [Fact]
    public async Task UpdatePOAsync_MissingOrderIsRejected()
    {
        using var s=await Scope.CreateAsync();await Assert.ThrowsAsync<NotFoundException>(()=>s.Api.UpdatePOAsync(999,UpdateRequest()));
    }

    // UTC-PO-018: Prevent editing an issued purchase order.
    [Fact]
    public async Task UpdatePOAsync_NonDraftOrderIsRejected()
    {
        using var s=await Scope.CreateAsync();await s.OrderAsync(status:"Chờ nhận");
        await Assert.ThrowsAsync<BadRequestException>(()=>s.Api.UpdatePOAsync(1,UpdateRequest()));
        s.Db.ChangeTracker.Clear();Assert.Equal(500000m,(await s.Db.PurchaseOrders.SingleAsync()).TotalAmount);
    }

    // UTC-PO-019: Reject an empty update without deleting existing details.
    [Fact]
    public async Task UpdatePOAsync_EmptyItemsAreRejectedWithoutSavingChanges()
    {
        using var s=await Scope.CreateAsync();await s.OrderAsync();var request=UpdateRequest();request.Items.Clear();
        await Assert.ThrowsAsync<BadRequestException>(()=>s.Api.UpdatePOAsync(1,request));
        s.Db.ChangeTracker.Clear();Assert.Equal(500000m,(await s.Db.PurchaseOrders.SingleAsync()).TotalAmount);
        Assert.Single(await s.Db.Podetails.ToListAsync());
    }

    // UTC-PO-020: Reject a missing supplier, past date or unsupported update status.
    [Fact]
    public async Task UpdatePOAsync_InvalidSupplierDateOrStatusIsRejected()
    {
        using var s=await Scope.CreateAsync();await s.OrderAsync();var request=UpdateRequest();request.SupplierId=999;
        await Assert.ThrowsAsync<NotFoundException>(()=>s.Api.UpdatePOAsync(1,request));
        request=UpdateRequest();request.DeliveryDate=Today.AddDays(-1);
        await Assert.ThrowsAsync<BadRequestException>(()=>s.Api.UpdatePOAsync(1,request));
        request=UpdateRequest();request.Status="UNKNOWN";
        await Assert.ThrowsAsync<BadRequestException>(()=>s.Api.UpdatePOAsync(1,request));
        s.Db.ChangeTracker.Clear();Assert.Equal(500000m,(await s.Db.PurchaseOrders.SingleAsync()).TotalAmount);
        Assert.Equal(1,await s.Db.Podetails.CountAsync());
    }

    // UTC-PO-021: Delete a draft purchase order together with its details.
    [Fact]
    public async Task DeletePOAsync_DraftOrderAndItsLinesAreDeleted()
    {
        using var s=await Scope.CreateAsync();await s.OrderAsync();await s.Api.DeletePOAsync(1);
        s.Db.ChangeTracker.Clear();Assert.False(await s.Db.PurchaseOrders.AnyAsync());Assert.False(await s.Db.Podetails.AnyAsync());
    }

    // UTC-PO-022: Reject deleting a nonexistent purchase order.
    [Fact]
    public async Task DeletePOAsync_MissingOrderIsRejected()
    {
        using var s=await Scope.CreateAsync();await Assert.ThrowsAsync<NotFoundException>(()=>s.Api.DeletePOAsync(999));
    }

    // UTC-PO-023: Prevent deleting a completed purchase order.
    [Fact]
    public async Task DeletePOAsync_NonDraftOrderIsRejected()
    {
        using var s=await Scope.CreateAsync();await s.OrderAsync(status:"Hoàn thành");
        await Assert.ThrowsAsync<BadRequestException>(()=>s.Api.DeletePOAsync(1));
        s.Db.ChangeTracker.Clear();Assert.Single(await s.Db.PurchaseOrders.ToListAsync());Assert.Single(await s.Db.Podetails.ToListAsync());
    }

    // UTC-PO-024: Accept supported draft-status aliases when deleting.
    [Fact]
    public async Task DeletePOAsync_SupportedDraftAliasesCanBeDeleted()
    {
        foreach(var status in new[]{"DRAFT","Mới tạo"}) { using var s=await Scope.CreateAsync();await s.OrderAsync(status:status);
            await s.Api.DeletePOAsync(1);s.Db.ChangeTracker.Clear();Assert.False(await s.Db.PurchaseOrders.AnyAsync()); }
    }

    // UTC-PO-025: Delete only the selected order and preserve other orders and details.
    [Fact]
    public async Task DeletePOAsync_OtherOrdersAndLinesRemainUnchanged()
    {
        using var s=await Scope.CreateAsync();await s.OrderAsync();await s.OrderAsync(2,herb:2);
        await s.Api.DeletePOAsync(1);s.Db.ChangeTracker.Clear();
        Assert.Equal(2,(await s.Db.PurchaseOrders.SingleAsync()).Poid);
        Assert.Equal(2,(await s.Db.Podetails.SingleAsync()).Poid);
    }

}

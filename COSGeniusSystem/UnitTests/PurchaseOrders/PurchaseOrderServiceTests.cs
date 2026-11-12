using COSGeniusSystem.Data;
using COSGeniusSystem.DTOs;
using COSGeniusSystem.Exceptions;
using COSGeniusSystem.Models;
using COSGeniusSystem.Services;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace COSGeniusSystem.UnitTests;

public class PurchaseOrderServiceTests
{
    private static ApplicationDbContext CreateInMemoryDbContext()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(databaseName: $"ClinicHerbDb_PO_{Guid.NewGuid()}")
            .Options;

        return new ApplicationDbContext(options);
    }

    private static async Task SeedCommonAsync(ApplicationDbContext context)
    {
        context.Suppliers.AddRange(
            new Supplier { SupplierId = 1, SupplierCode = "NCC00001", SupplierName = "Công ty Dược liệu An Phát", IsActive = true },
            new Supplier { SupplierId = 2, SupplierCode = "NCC00002", SupplierName = "Công ty Thảo Dược Minh Tâm", IsActive = true }
        );

        context.Herbs.AddRange(
            new Herb { HerbId = 1, HerbCode = "VT00001", CommonName = "Cam thảo", Unit = "kg", IsActive = true },
            new Herb { HerbId = 2, HerbCode = "VT00002", CommonName = "Đỗ trọng", Unit = "kg", IsActive = true }
        );

        context.Users.Add(
            new User { UserId = 1, Username = "manager", FullName = "Quản lý kho", PasswordHash = "test_hash", IsActive = true }
        );

        await context.SaveChangesAsync();
    }

    #region Group 1: GetPurchaseOrdersAsync

    [Fact]
    public async Task UTC001_GetPurchaseOrdersAsync_NoFilter_ReturnsAllPOs()
    {
        using var context = CreateInMemoryDbContext();
        await SeedCommonAsync(context);

        context.PurchaseOrders.AddRange(
            new PurchaseOrder { Poid = 1, Pocode = "PO00001", SupplierId = 1, CreatedBy = 1, Status = "Nháp", TotalAmount = 500000, OrderDate = DateTime.Now.AddDays(-2) },
            new PurchaseOrder { Poid = 2, Pocode = "PO00002", SupplierId = 2, CreatedBy = 1, Status = "Hoàn thành", TotalAmount = 1000000, OrderDate = DateTime.Now.AddDays(-1) }
        );
        await context.SaveChangesAsync();

        var service = new PurchaseOrderService(context);
        var result = await service.GetPurchaseOrdersAsync();

        Assert.Equal(2, result.Count);
    }

    [Fact]
    public async Task UTC002_GetPurchaseOrdersAsync_FilterByStatusPending_ReturnsPendingPOs()
    {
        using var context = CreateInMemoryDbContext();
        await SeedCommonAsync(context);

        context.PurchaseOrders.AddRange(
            new PurchaseOrder { Poid = 1, Pocode = "PO00001", SupplierId = 1, CreatedBy = 1, Status = "Chờ nhận", TotalAmount = 500000, OrderDate = DateTime.Now.AddDays(-2) },
            new PurchaseOrder { Poid = 2, Pocode = "PO00002", SupplierId = 2, CreatedBy = 1, Status = "Hoàn thành", TotalAmount = 1000000, OrderDate = DateTime.Now.AddDays(-1) }
        );
        await context.SaveChangesAsync();

        var service = new PurchaseOrderService(context);
        var result = await service.GetPurchaseOrdersAsync(status: "chờ nhận");

        Assert.Single(result);
        Assert.Equal("PO00001", result[0].Pocode);
    }

    [Fact]
    public async Task UTC003_GetPurchaseOrdersAsync_SearchByPOCode_ReturnsMatchingPO()
    {
        using var context = CreateInMemoryDbContext();
        await SeedCommonAsync(context);

        context.PurchaseOrders.AddRange(
            new PurchaseOrder { Poid = 1, Pocode = "PO00001", SupplierId = 1, CreatedBy = 1, Status = "Nháp", TotalAmount = 500000, OrderDate = DateTime.Now.AddDays(-2) },
            new PurchaseOrder { Poid = 2, Pocode = "PO00002", SupplierId = 2, CreatedBy = 1, Status = "Nháp", TotalAmount = 1000000, OrderDate = DateTime.Now.AddDays(-1) }
        );
        await context.SaveChangesAsync();

        var service = new PurchaseOrderService(context);
        var result = await service.GetPurchaseOrdersAsync(search: "PO00001");

        Assert.Single(result);
        Assert.Equal("PO00001", result[0].Pocode);
    }

    [Fact]
    public async Task UTC004_GetPurchaseOrdersAsync_SearchBySupplierName_ReturnsMatchingPO()
    {
        using var context = CreateInMemoryDbContext();
        await SeedCommonAsync(context);

        context.PurchaseOrders.AddRange(
            new PurchaseOrder { Poid = 1, Pocode = "PO00001", SupplierId = 1, CreatedBy = 1, Status = "Nháp", TotalAmount = 500000, OrderDate = DateTime.Now.AddDays(-2) },
            new PurchaseOrder { Poid = 2, Pocode = "PO00002", SupplierId = 2, CreatedBy = 1, Status = "Nháp", TotalAmount = 1000000, OrderDate = DateTime.Now.AddDays(-1) }
        );
        await context.SaveChangesAsync();

        var service = new PurchaseOrderService(context);
        var result = await service.GetPurchaseOrdersAsync(search: "An Phát");

        Assert.Single(result);
        Assert.Equal("Công ty Dược liệu An Phát", result[0].SupplierName);
    }

    [Fact]
    public async Task UTC005_GetPurchaseOrdersAsync_EmptyDatabase_ReturnsEmptyList()
    {
        using var context = CreateInMemoryDbContext();
        var service = new PurchaseOrderService(context);

        var result = await service.GetPurchaseOrdersAsync();

        Assert.Empty(result);
    }

    #endregion

    #region Group 2: GetPurchaseOrderDetailAsync

    [Fact]
    public async Task UTC006_GetPurchaseOrderDetailAsync_ValidPoid_ReturnsPODetail()
    {
        using var context = CreateInMemoryDbContext();
        await SeedCommonAsync(context);

        var po = new PurchaseOrder { Poid = 1, Pocode = "PO00001", SupplierId = 1, CreatedBy = 1, Status = "Nháp", TotalAmount = 500000, OrderDate = DateTime.Now };
        po.Podetails.Add(new Podetail { HerbId = 1, OrderQuantity = 10, UnitPrice = 50000 });
        context.PurchaseOrders.Add(po);
        await context.SaveChangesAsync();

        var service = new PurchaseOrderService(context);
        var result = await service.GetPurchaseOrderDetailAsync("1");

        Assert.NotNull(result);
        Assert.Equal(1, result.Poid);
        Assert.Equal("PO00001", result.Pocode);
        Assert.Single(result.Details);
    }

    [Fact]
    public async Task UTC007_GetPurchaseOrderDetailAsync_ValidPOCode_ReturnsPODetail()
    {
        using var context = CreateInMemoryDbContext();
        await SeedCommonAsync(context);

        var po = new PurchaseOrder { Poid = 1, Pocode = "PO00001", SupplierId = 1, CreatedBy = 1, Status = "Nháp", TotalAmount = 500000, OrderDate = DateTime.Now };
        context.PurchaseOrders.Add(po);
        await context.SaveChangesAsync();

        var service = new PurchaseOrderService(context);
        var result = await service.GetPurchaseOrderDetailAsync("PO00001");

        Assert.NotNull(result);
        Assert.Equal("PO00001", result.Pocode);
    }

    [Fact]
    public async Task UTC008_GetPurchaseOrderDetailAsync_NonExistingId_ReturnsNull()
    {
        using var context = CreateInMemoryDbContext();
        var service = new PurchaseOrderService(context);

        var result = await service.GetPurchaseOrderDetailAsync("999");

        Assert.Null(result);
    }

    #endregion

    #region Group 3: CreatePOAsync

    [Fact]
    public async Task UTC009_CreatePOAsync_ValidData_CreatesSuccessfully()
    {
        using var context = CreateInMemoryDbContext();
        await SeedCommonAsync(context);

        var request = new CreatePORequest
        {
            SupplierId = 1,
            DeliveryDate = DateOnly.FromDateTime(DateTime.Now.AddDays(7)),
            Status = "Nháp",
            Items = new List<CreatePOItemRequest>
            {
                new() { HerbId = 1, OrderQuantity = 10, UnitPrice = 50000 },
                new() { HerbId = 2, OrderQuantity = 5, UnitPrice = 100000 }
            }
        };

        var service = new PurchaseOrderService(context);
        var result = await service.CreatePOAsync(1, request);

        Assert.NotNull(result);
        Assert.Equal("PO00001", result.Pocode);
        Assert.Equal(1000000, result.TotalAmount);
        Assert.Equal(2, result.Details.Count);
        Assert.Equal("Nháp", result.Status);
    }

    [Fact]
    public async Task UTC010_CreatePOAsync_SupplierNotFound_ThrowsNotFoundException()
    {
        using var context = CreateInMemoryDbContext();
        await SeedCommonAsync(context);

        var request = new CreatePORequest
        {
            SupplierId = 999, // Không tồn tại
            Items = new List<CreatePOItemRequest>
            {
                new() { HerbId = 1, OrderQuantity = 10, UnitPrice = 50000 }
            }
        };

        var service = new PurchaseOrderService(context);
        var ex = await Assert.ThrowsAsync<NotFoundException>(() => service.CreatePOAsync(1, request));
        Assert.Contains("999", ex.Message);
    }

    [Fact]
    public async Task UTC011_CreatePOAsync_EmptyItems_ThrowsBadRequestException()
    {
        using var context = CreateInMemoryDbContext();
        await SeedCommonAsync(context);

        var request = new CreatePORequest
        {
            SupplierId = 1,
            Items = new List<CreatePOItemRequest>()
        };

        var service = new PurchaseOrderService(context);
        var ex = await Assert.ThrowsAsync<BadRequestException>(() => service.CreatePOAsync(1, request));
        Assert.Equal("Đơn mua hàng phải có ít nhất một mặt hàng vị thuốc.", ex.Message);
    }

    [Fact]
    public async Task UTC012_CreatePOAsync_ZeroOrderQuantity_ThrowsBadRequestException()
    {
        using var context = CreateInMemoryDbContext();
        await SeedCommonAsync(context);

        var request = new CreatePORequest
        {
            SupplierId = 1,
            Items = new List<CreatePOItemRequest>
            {
                new() { HerbId = 1, OrderQuantity = 0, UnitPrice = 50000 }
            }
        };

        var service = new PurchaseOrderService(context);
        var ex = await Assert.ThrowsAsync<BadRequestException>(() => service.CreatePOAsync(1, request));
        Assert.Equal("Số lượng đặt phải lớn hơn 0.", ex.Message);
    }

    [Fact]
    public async Task UTC013_CreatePOAsync_NegativeOrderQuantity_ThrowsBadRequestException()
    {
        using var context = CreateInMemoryDbContext();
        await SeedCommonAsync(context);

        var request = new CreatePORequest
        {
            SupplierId = 1,
            Items = new List<CreatePOItemRequest>
            {
                new() { HerbId = 1, OrderQuantity = -5, UnitPrice = 50000 }
            }
        };

        var service = new PurchaseOrderService(context);
        var ex = await Assert.ThrowsAsync<BadRequestException>(() => service.CreatePOAsync(1, request));
        Assert.Equal("Số lượng đặt phải lớn hơn 0.", ex.Message);
    }

    [Fact]
    public async Task UTC014_CreatePOAsync_NegativeUnitPrice_ThrowsBadRequestException()
    {
        using var context = CreateInMemoryDbContext();
        await SeedCommonAsync(context);

        var request = new CreatePORequest
        {
            SupplierId = 1,
            Items = new List<CreatePOItemRequest>
            {
                new() { HerbId = 1, OrderQuantity = 5, UnitPrice = -1000 }
            }
        };

        var service = new PurchaseOrderService(context);
        var ex = await Assert.ThrowsAsync<BadRequestException>(() => service.CreatePOAsync(1, request));
        Assert.Equal("Đơn giá không được âm.", ex.Message);
    }

    #endregion

    #region Group 4: UpdatePOAsync

    [Fact]
    public async Task UTC015_UpdatePOAsync_DraftPO_UpdatesSuccessfully()
    {
        using var context = CreateInMemoryDbContext();
        await SeedCommonAsync(context);

        var po = new PurchaseOrder { Poid = 1, Pocode = "PO00001", SupplierId = 1, CreatedBy = 1, Status = "Nháp", TotalAmount = 500000, OrderDate = DateTime.Now };
        po.Podetails.Add(new Podetail { HerbId = 1, OrderQuantity = 10, UnitPrice = 50000 });
        context.PurchaseOrders.Add(po);
        await context.SaveChangesAsync();

        var request = new UpdatePORequest
        {
            SupplierId = 1,
            DeliveryDate = DateOnly.FromDateTime(DateTime.Now.AddDays(10)),
            Items = new List<CreatePOItemRequest>
            {
                new() { HerbId = 1, OrderQuantity = 20, UnitPrice = 50000 }
            }
        };

        var service = new PurchaseOrderService(context);
        var result = await service.UpdatePOAsync(1, request);

        Assert.NotNull(result);
        Assert.Equal(1000000, result.TotalAmount);
        Assert.Equal(20, result.Details[0].OrderQuantity);
    }

    [Fact]
    public async Task UTC016_UpdatePOAsync_PONotFound_ThrowsNotFoundException()
    {
        using var context = CreateInMemoryDbContext();
        await SeedCommonAsync(context);

        var request = new UpdatePORequest
        {
            SupplierId = 1,
            Items = new List<CreatePOItemRequest>
            {
                new() { HerbId = 1, OrderQuantity = 10, UnitPrice = 50000 }
            }
        };

        var service = new PurchaseOrderService(context);
        var ex = await Assert.ThrowsAsync<NotFoundException>(() => service.UpdatePOAsync(999, request));
        Assert.Contains("999", ex.Message);
    }

    [Fact]
    public async Task UTC017_UpdatePOAsync_NonDraftStatus_ThrowsBadRequestException()
    {
        using var context = CreateInMemoryDbContext();
        await SeedCommonAsync(context);

        var po = new PurchaseOrder { Poid = 1, Pocode = "PO00001", SupplierId = 1, CreatedBy = 1, Status = "Đã nhận hàng", TotalAmount = 500000, OrderDate = DateTime.Now };
        context.PurchaseOrders.Add(po);
        await context.SaveChangesAsync();

        var request = new UpdatePORequest
        {
            SupplierId = 1,
            Items = new List<CreatePOItemRequest>
            {
                new() { HerbId = 1, OrderQuantity = 10, UnitPrice = 50000 }
            }
        };

        var service = new PurchaseOrderService(context);
        var ex = await Assert.ThrowsAsync<BadRequestException>(() => service.UpdatePOAsync(1, request));
        Assert.Contains("Chỉ được phép chỉnh sửa đơn mua hàng ở trạng thái 'Nháp'", ex.Message);
    }

    [Fact]
    public async Task UTC018_UpdatePOAsync_EmptyItems_ThrowsBadRequestException()
    {
        using var context = CreateInMemoryDbContext();
        await SeedCommonAsync(context);

        var po = new PurchaseOrder { Poid = 1, Pocode = "PO00001", SupplierId = 1, CreatedBy = 1, Status = "Nháp", TotalAmount = 500000, OrderDate = DateTime.Now };
        context.PurchaseOrders.Add(po);
        await context.SaveChangesAsync();

        var request = new UpdatePORequest
        {
            SupplierId = 1,
            Items = new List<CreatePOItemRequest>()
        };

        var service = new PurchaseOrderService(context);
        var ex = await Assert.ThrowsAsync<BadRequestException>(() => service.UpdatePOAsync(1, request));
        Assert.Equal("Đơn mua hàng phải có ít nhất một mặt hàng vị thuốc.", ex.Message);
    }

    [Fact]
    public async Task UTC019_UpdatePOAsync_SupplierNotFound_ThrowsNotFoundException()
    {
        using var context = CreateInMemoryDbContext();
        await SeedCommonAsync(context);

        var po = new PurchaseOrder { Poid = 1, Pocode = "PO00001", SupplierId = 1, CreatedBy = 1, Status = "Nháp", TotalAmount = 500000, OrderDate = DateTime.Now };
        context.PurchaseOrders.Add(po);
        await context.SaveChangesAsync();

        var request = new UpdatePORequest
        {
            SupplierId = 888, // Không tồn tại
            Items = new List<CreatePOItemRequest>
            {
                new() { HerbId = 1, OrderQuantity = 10, UnitPrice = 50000 }
            }
        };

        var service = new PurchaseOrderService(context);
        var ex = await Assert.ThrowsAsync<NotFoundException>(() => service.UpdatePOAsync(1, request));
        Assert.Contains("888", ex.Message);
    }

    #endregion

    #region Group 5: DeletePOAsync

    [Fact]
    public async Task UTC020_DeletePOAsync_DraftPO_DeletesSuccessfully()
    {
        using var context = CreateInMemoryDbContext();
        await SeedCommonAsync(context);

        var po = new PurchaseOrder { Poid = 1, Pocode = "PO00001", SupplierId = 1, CreatedBy = 1, Status = "Nháp", TotalAmount = 500000, OrderDate = DateTime.Now };
        po.Podetails.Add(new Podetail { HerbId = 1, OrderQuantity = 10, UnitPrice = 50000 });
        context.PurchaseOrders.Add(po);
        await context.SaveChangesAsync();

        var service = new PurchaseOrderService(context);
        await service.DeletePOAsync(1);

        var deleted = await context.PurchaseOrders.FindAsync(1);
        Assert.Null(deleted);
        var details = await context.Podetails.Where(d => d.Poid == 1).ToListAsync();
        Assert.Empty(details);
    }

    [Fact]
    public async Task UTC021_DeletePOAsync_PONotFound_ThrowsNotFoundException()
    {
        using var context = CreateInMemoryDbContext();
        var service = new PurchaseOrderService(context);

        var ex = await Assert.ThrowsAsync<NotFoundException>(() => service.DeletePOAsync(999));
        Assert.Contains("999", ex.Message);
    }

    [Fact]
    public async Task UTC022_DeletePOAsync_NonDraftPO_ThrowsBadRequestException()
    {
        using var context = CreateInMemoryDbContext();
        await SeedCommonAsync(context);

        var po = new PurchaseOrder { Poid = 1, Pocode = "PO00001", SupplierId = 1, CreatedBy = 1, Status = "Hoàn thành", TotalAmount = 500000, OrderDate = DateTime.Now };
        context.PurchaseOrders.Add(po);
        await context.SaveChangesAsync();

        var service = new PurchaseOrderService(context);
        var ex = await Assert.ThrowsAsync<BadRequestException>(() => service.DeletePOAsync(1));
        Assert.Contains("Chỉ được phép xóa đơn mua hàng ở trạng thái 'Nháp'", ex.Message);
    }

    #endregion
}

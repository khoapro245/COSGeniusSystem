using COSGeniusSystem.Data;
using COSGeniusSystem.DTOs.Suppliers;
using COSGeniusSystem.Models;
using COSGeniusSystem.Services.Suppliers;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace COSGeniusSystem.UnitTests.Suppliers;

public class SupplierServiceTests
{
    private static ApplicationDbContext CreateInMemoryDbContext()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(databaseName: $"ClinicHerbDb_Supplier_{Guid.NewGuid()}")
            .Options;

        return new ApplicationDbContext(options);
    }

    [Fact]
    public async Task GetSuppliersAsync_FilterByIsActive_ReturnsMatchingSuppliers()
    {
        using var context = CreateInMemoryDbContext();
        context.Suppliers.AddRange(
            new Supplier { SupplierId = 1, SupplierCode = "NCC00001", SupplierName = "Dược Liệu An Phát", IsActive = true },
            new Supplier { SupplierId = 2, SupplierCode = "NCC00002", SupplierName = "Thảo Dược Minh Tâm", IsActive = true },
            new Supplier { SupplierId = 3, SupplierCode = "NCC00003", SupplierName = "Đông Y Cổ Truyền", IsActive = false }
        );
        await context.SaveChangesAsync();

        var service = new SupplierService(context);
        var result = await service.GetSuppliersAsync(new SupplierFilterQuery { IsActive = true });

        Assert.Equal(2, result.Count);
        Assert.All(result, s => Assert.True(s.IsActive));
        Assert.DoesNotContain(result, s => s.SupplierId == 3);
    }

    [Fact]
    public async Task GetSuppliersAsync_KeywordSearch_ReturnsMatchingSuppliers()
    {
        using var context = CreateInMemoryDbContext();
        context.Suppliers.AddRange(
            new Supplier { SupplierId = 1, SupplierCode = "NCC00001", SupplierName = "Dược Liệu An Phát", Phone = "0912345678", IsActive = true },
            new Supplier { SupplierId = 2, SupplierCode = "NCC00002", SupplierName = "Thảo Dược Minh Tâm", Phone = "0987654321", IsActive = true }
        );
        await context.SaveChangesAsync();

        var service = new SupplierService(context);
        var result = await service.GetSuppliersAsync(new SupplierFilterQuery { Keyword = "An Phát" });

        Assert.Single(result);
        Assert.Equal("NCC00001", result[0].SupplierCode);
    }

    [Fact]
    public async Task GetSupplierByIdAsync_ValidId_ReturnsDetailWithOrders()
    {
        using var context = CreateInMemoryDbContext();
        var supplier = new Supplier
        {
            SupplierId = 1,
            SupplierCode = "NCC00001",
            SupplierName = "Dược Liệu An Phát",
            Phone = "0912345678",
            Email = "contact@anphat.vn",
            Address = "Hà Nội",
            TaxCode = "0101234567",
            IsActive = true
        };
        context.Suppliers.Add(supplier);

        context.PurchaseOrders.Add(new PurchaseOrder
        {
            Poid = 101,
            Pocode = "PO00001",
            SupplierId = 1,
            CreatedBy = 1,
            OrderDate = DateTime.UtcNow,
            TotalAmount = 5000000m,
            Status = "Hoàn tất"
        });

        await context.SaveChangesAsync();

        var service = new SupplierService(context);
        var detail = await service.GetSupplierByIdAsync(1);

        Assert.NotNull(detail);
        Assert.Equal("NCC00001", detail.SupplierCode);
        Assert.Single(detail.PurchaseOrders);
        Assert.Equal("PO00001", detail.PurchaseOrders[0].PoCode);
    }

    [Fact]
    public async Task CreateSupplierAsync_WithAutoCode_GeneratesCodeAndSaves()
    {
        using var context = CreateInMemoryDbContext();
        var service = new SupplierService(context);

        var request = new CreateSupplierRequest
        {
            SupplierName = "Công ty Dược Liệu Xanh",
            Phone = "0933112233",
            Email = "xanh@duoclieu.vn",
            Address = "TP. Hồ Chí Minh",
            TaxCode = "0309998888",
            IsActive = true
        };

        var (dto, error) = await service.CreateSupplierAsync(request);

        Assert.Null(error);
        Assert.NotNull(dto);
        Assert.StartsWith("NCC", dto.SupplierCode);
        Assert.Equal("Công ty Dược Liệu Xanh", dto.SupplierName);

        var count = await context.Suppliers.CountAsync();
        Assert.Equal(1, count);
    }

    [Fact]
    public async Task CreateSupplierAsync_DuplicateCode_ReturnsError()
    {
        using var context = CreateInMemoryDbContext();
        context.Suppliers.Add(new Supplier
        {
            SupplierId = 1,
            SupplierCode = "NCC00001",
            SupplierName = "NCC Hiện Tại",
            IsActive = true
        });
        await context.SaveChangesAsync();

        var service = new SupplierService(context);
        var request = new CreateSupplierRequest
        {
            SupplierCode = "NCC00001",
            SupplierName = "NCC Trùng Mã"
        };

        var (dto, error) = await service.CreateSupplierAsync(request);

        Assert.Null(dto);
        Assert.NotNull(error);
        Assert.Contains("đã tồn tại", error);
    }

    [Fact]
    public async Task UpdateSupplierAsync_ValidRequest_UpdatesFields()
    {
        using var context = CreateInMemoryDbContext();
        context.Suppliers.Add(new Supplier
        {
            SupplierId = 5,
            SupplierCode = "NCC00005",
            SupplierName = "Tên Cũ",
            Phone = "0111",
            IsActive = true
        });
        await context.SaveChangesAsync();

        var service = new SupplierService(context);
        var updateRequest = new UpdateSupplierRequest
        {
            SupplierName = "Tên Mới Cập Nhật",
            Phone = "0999888777",
            Email = "new@supplier.vn",
            Address = "Đà Nẵng",
            TaxCode = "0401122334",
            IsActive = false
        };

        var (dto, error) = await service.UpdateSupplierAsync(5, updateRequest);

        Assert.Null(error);
        Assert.NotNull(dto);
        Assert.Equal("Tên Mới Cập Nhật", dto.SupplierName);
        Assert.Equal("0999888777", dto.Phone);
        Assert.False(dto.IsActive);
    }

    [Fact]
    public async Task DeleteSupplierAsync_WithoutOrders_HardDeletesSupplier()
    {
        using var context = CreateInMemoryDbContext();
        context.Suppliers.Add(new Supplier
        {
            SupplierId = 10,
            SupplierCode = "NCC00010",
            SupplierName = "NCC Chưa Có Đơn",
            IsActive = true
        });
        await context.SaveChangesAsync();

        var service = new SupplierService(context);
        var (success, isDeactivated, error) = await service.DeleteSupplierAsync(10);

        Assert.True(success);
        Assert.False(isDeactivated);
        Assert.Null(error);

        var exists = await context.Suppliers.AnyAsync(s => s.SupplierId == 10);
        Assert.False(exists);
    }

    [Fact]
    public async Task DeleteSupplierAsync_WithOrders_SoftDeactivatesSupplier()
    {
        using var context = CreateInMemoryDbContext();
        context.Suppliers.Add(new Supplier
        {
            SupplierId = 20,
            SupplierCode = "NCC00020",
            SupplierName = "NCC Đã Có Đơn",
            IsActive = true
        });
        context.PurchaseOrders.Add(new PurchaseOrder
        {
            Poid = 501,
            Pocode = "PO00501",
            SupplierId = 20,
            CreatedBy = 1,
            OrderDate = DateTime.UtcNow,
            TotalAmount = 10000000m,
            Status = "Hoàn tất"
        });
        await context.SaveChangesAsync();

        var service = new SupplierService(context);
        var (success, isDeactivated, error) = await service.DeleteSupplierAsync(20);

        Assert.True(success);
        Assert.True(isDeactivated);
        Assert.Null(error);

        var saved = await context.Suppliers.FindAsync(20);
        Assert.NotNull(saved);
        Assert.False(saved.IsActive);
    }
}


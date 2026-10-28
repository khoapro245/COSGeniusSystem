using COSGeniusSystem.Data;
using COSGeniusSystem.DTOs.Herbs;
using COSGeniusSystem.Models;
using COSGeniusSystem.Services.Herbs;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace COSGeniusSystem.UnitTests.Herbs;

public class HerbServiceTests
{
    private static ApplicationDbContext CreateInMemoryDbContext()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(databaseName: $"ClinicHerbDb_Herb_{Guid.NewGuid()}")
            .Options;

        return new ApplicationDbContext(options);
    }

    #region UT-TU-01: Test danh mục vị thuốc

    [Fact]
    public async Task GetHerbsAsync_FilterByIsActive_ReturnsOnlyActiveHerbs()
    {
        // Arrange
        using var context = CreateInMemoryDbContext();
        context.Herbs.AddRange(
            new Herb { HerbId = 1, HerbCode = "VT001", CommonName = "Cam thảo", Unit = "g", StandardPrice = 5000m, MinThreshold = 100m, IsActive = true },
            new Herb { HerbId = 2, HerbCode = "VT002", CommonName = "Hoàng kỳ", Unit = "g", StandardPrice = 8000m, MinThreshold = 50m, IsActive = true },
            new Herb { HerbId = 3, HerbCode = "VT003", CommonName = "Nhân sâm (Ngừng NK)", Unit = "g", StandardPrice = 200000m, MinThreshold = 10m, IsActive = false }
        );
        await context.SaveChangesAsync();

        var service = new HerbService(context);
        var query = new HerbFilterQuery { IsActive = true };

        // Act
        var result = await service.GetHerbsAsync(query);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(2, result.Count);
        Assert.All(result, h => Assert.True(h.IsActive));
        Assert.DoesNotContain(result, h => h.HerbId == 3);
    }

    [Fact]
    public async Task GetHerbsAsync_FilterByKeyword_ReturnsMatchingHerbs()
    {
        // Arrange
        // Dùng chữ thường để tránh vấn đề case-sensitivity trên EF InMemory provider
        using var context = CreateInMemoryDbContext();
        context.Herbs.AddRange(
            new Herb { HerbId = 1, HerbCode = "VT001", CommonName = "cam thảo bắc", Unit = "g", StandardPrice = 5000m, MinThreshold = 100m, IsActive = true },
            new Herb { HerbId = 2, HerbCode = "VT002", CommonName = "hoàng kỳ", Unit = "g", StandardPrice = 8000m, MinThreshold = 50m, IsActive = true },
            new Herb { HerbId = 3, HerbCode = "VT003", CommonName = "cam thảo nam", Unit = "g", StandardPrice = 3000m, MinThreshold = 100m, IsActive = true }
        );
        await context.SaveChangesAsync();

        var service = new HerbService(context);
        var query = new HerbFilterQuery { Keyword = "cam thảo", IsActive = true };

        // Act
        var result = await service.GetHerbsAsync(query);

        // Assert: Keyword "cam thảo" match CommonName "cam thảo bắc" và "cam thảo nam"
        Assert.NotNull(result);
        Assert.Equal(2, result.Count);
        Assert.DoesNotContain(result, h => h.HerbId == 2); // "hoàng kỳ" không match
        Assert.All(result, h => Assert.Contains("thảo", h.CommonName, StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task GetHerbsAsync_WithPaging_ReturnsCorrectPage()
    {
        // Arrange: 5 vị thuốc, lấy page 2 với pageSize = 2 → phải trả về 2 bản ghi
        using var context = CreateInMemoryDbContext();
        context.Herbs.AddRange(
            new Herb { HerbId = 1, HerbCode = "A001", CommonName = "Bạch linh", Unit = "g", StandardPrice = 10000m, MinThreshold = 0, IsActive = true },
            new Herb { HerbId = 2, HerbCode = "A002", CommonName = "Cam thảo", Unit = "g", StandardPrice = 5000m, MinThreshold = 0, IsActive = true },
            new Herb { HerbId = 3, HerbCode = "A003", CommonName = "Đương quy", Unit = "g", StandardPrice = 15000m, MinThreshold = 0, IsActive = true },
            new Herb { HerbId = 4, HerbCode = "A004", CommonName = "Hà thủ ô", Unit = "g", StandardPrice = 12000m, MinThreshold = 0, IsActive = true },
            new Herb { HerbId = 5, HerbCode = "A005", CommonName = "Ý dĩ", Unit = "g", StandardPrice = 7000m, MinThreshold = 0, IsActive = true }
        );
        await context.SaveChangesAsync();

        var service = new HerbService(context);
        var query = new HerbFilterQuery { IsActive = true, Page = 2, PageSize = 2 };

        // Act
        var result = await service.GetHerbsAsync(query);

        // Assert: Trang 2 có 2 bản ghi (OrderBy CommonName: Bạch linh, Cam thảo | Đương quy, Hà thủ ô | Ý dĩ)
        Assert.NotNull(result);
        Assert.Equal(2, result.Count);
    }

    #endregion

    #region UT-TU-02: Test chi tiết vị thuốc

    [Fact]
    public async Task GetHerbByIdAsync_NonExistingId_ReturnsNull()
    {
        // Arrange
        using var context = CreateInMemoryDbContext();
        context.Herbs.Add(new Herb { HerbId = 1, HerbCode = "VT001", CommonName = "Cam thảo", Unit = "g", StandardPrice = 5000m, MinThreshold = 0, IsActive = true });
        await context.SaveChangesAsync();

        var service = new HerbService(context);

        // Act
        var result = await service.GetHerbByIdAsync(99999);

        // Assert: Trả về null để Controller trả 404
        Assert.Null(result);
    }

    [Fact]
    public async Task GetHerbByIdAsync_ExistingId_ReturnsMappedHerbDetail()
    {
        // Arrange
        using var context = CreateInMemoryDbContext();
        var herb = new Herb
        {
            HerbId = 1,
            HerbCode = "VT001",
            CommonName = "Cam thảo bắc",
            ScientificName = "Glycyrrhiza uralensis",
            Unit = "g",
            TasteProperties = "Vị ngọt, tính bình; quy kinh Tâm, Phế, Tỳ, Vị",
            StandardPrice = 5000m,
            MinThreshold = 200m,
            IsActive = true
        };
        context.Herbs.Add(herb);
        await context.SaveChangesAsync();

        var service = new HerbService(context);

        // Act
        var result = await service.GetHerbByIdAsync(herb.HerbId);

        // Assert: Map đúng toàn bộ thông tin
        Assert.NotNull(result);
        Assert.Equal(herb.HerbId, result.HerbId);
        Assert.Equal(herb.HerbCode, result.HerbCode);
        Assert.Equal(herb.CommonName, result.CommonName);
        Assert.Equal(herb.ScientificName, result.ScientificName);
        Assert.Equal(herb.Unit, result.Unit);
        Assert.Equal(herb.TasteProperties, result.TasteProperties);
        Assert.Equal(herb.StandardPrice, result.StandardPrice);
        Assert.Equal(herb.MinThreshold, result.MinThreshold);
        Assert.True(result.IsActive);
    }

    #endregion

    #region UT-TU-03: Test thêm mới vị thuốc

    [Theory]
    [InlineData(0)]
    [InlineData(-500)]
    public async Task CreateHerbAsync_PriceLessOrEqualToZero_ReturnsValidationError(decimal invalidPrice)
    {
        // Arrange
        using var context = CreateInMemoryDbContext();
        var service = new HerbService(context);

        var request = new CreateHerbRequest
        {
            HerbCode = "VT_TEST",
            CommonName = "Thủy xương bồ",
            Unit = "g",
            StandardPrice = invalidPrice
        };

        // Act
        var (dto, errorMessage) = await service.CreateHerbAsync(request);

        // Assert: Thất bại do giá không hợp lệ
        Assert.Null(dto);
        Assert.NotNull(errorMessage);
        Assert.Contains("StandardPrice must be greater than 0", errorMessage);

        // Đảm bảo không bản ghi nào được lưu
        Assert.Empty(await context.Herbs.ToListAsync());
    }

    [Fact]
    public async Task CreateHerbAsync_DuplicateHerbCodeOrName_ReturnsConflictError()
    {
        // Arrange: Seed sẵn một vị thuốc
        using var context = CreateInMemoryDbContext();
        context.Herbs.Add(new Herb
        {
            HerbId = 1,
            HerbCode = "VT001",
            CommonName = "Cam thảo",
            Unit = "g",
            StandardPrice = 5000m,
            MinThreshold = 100m,
            IsActive = true
        });
        await context.SaveChangesAsync();

        var service = new HerbService(context);

        var requestDuplicateName = new CreateHerbRequest
        {
            HerbCode = "VT999",
            CommonName = "Cam thảo", // Trùng tên
            Unit = "g",
            StandardPrice = 6000m
        };

        // Act
        var (dto, errorMessage) = await service.CreateHerbAsync(requestDuplicateName);

        // Assert: Báo lỗi trùng lặp
        Assert.Null(dto);
        Assert.NotNull(errorMessage);
        Assert.Contains("already exists", errorMessage);
    }

    [Fact]
    public async Task CreateHerbAsync_ValidData_CreatesSuccessfullyWithActiveStatus()
    {
        // Arrange: Dữ liệu hợp lệ
        using var context = CreateInMemoryDbContext();
        var service = new HerbService(context);

        var request = new CreateHerbRequest
        {
            HerbCode = "VT010",
            CommonName = "Đương quy",
            ScientificName = "Angelica sinensis",
            Unit = "g",
            TasteProperties = "Vị ngọt cay, tính ôn; quy kinh Can, Tâm, Tỳ",
            StandardPrice = 15000m,
            MinThreshold = 50m
        };

        // Act
        var (dto, errorMessage) = await service.CreateHerbAsync(request);

        // Assert: Tạo thành công
        Assert.Null(errorMessage);
        Assert.NotNull(dto);
        Assert.Equal("VT010", dto.HerbCode);
        Assert.Equal("Đương quy", dto.CommonName);
        Assert.Equal(15000m, dto.StandardPrice);
        Assert.True(dto.IsActive);

        // Kiểm tra lưu đúng trong Database
        var savedEntity = await context.Herbs.FirstOrDefaultAsync(h => h.HerbCode == "VT010");
        Assert.NotNull(savedEntity);
        Assert.True(savedEntity.IsActive);
        Assert.Equal(15000m, savedEntity.StandardPrice);
    }

    #endregion

    #region UT-TU-04: Test cập nhật vị thuốc

    [Fact]
    public async Task UpdateHerbAsync_NonExistingId_ReturnsNullTuple()
    {
        // Arrange
        using var context = CreateInMemoryDbContext();
        var service = new HerbService(context);

        var request = new UpdateHerbRequest
        {
            CommonName = "Không tồn tại",
            Unit = "g",
            StandardPrice = 5000m,
            IsActive = true
        };

        // Act
        var (dto, errorMessage) = await service.UpdateHerbAsync(99999, request);

        // Assert: (null, null) → Controller trả 404
        Assert.Null(dto);
        Assert.Null(errorMessage);
    }

    [Fact]
    public async Task UpdateHerbAsync_ValidRequest_UpdatesHerbSuccessfully()
    {
        // Arrange
        using var context = CreateInMemoryDbContext();
        var existing = new Herb
        {
            HerbId = 1,
            HerbCode = "VT001",
            CommonName = "Hoàng kỳ",
            Unit = "g",
            StandardPrice = 8000m,
            MinThreshold = 50m,
            IsActive = true
        };
        context.Herbs.Add(existing);
        await context.SaveChangesAsync();

        var service = new HerbService(context);

        var updateRequest = new UpdateHerbRequest
        {
            CommonName = "Hoàng kỳ chích mật",
            ScientificName = "Astragalus membranaceus",
            Unit = "g",
            TasteProperties = "Vị ngọt, tính ôn; quy kinh Phế, Tỳ",
            StandardPrice = 12000m, // Giá mới
            MinThreshold = 60m,
            IsActive = true
        };

        // Act
        var (dto, errorMessage) = await service.UpdateHerbAsync(existing.HerbId, updateRequest);

        // Assert: Cập nhật thành công
        Assert.Null(errorMessage);
        Assert.NotNull(dto);
        Assert.Equal(12000m, dto.StandardPrice);
        Assert.Equal("Hoàng kỳ chích mật", dto.CommonName);

        var inDb = await context.Herbs.FindAsync(existing.HerbId);
        Assert.NotNull(inDb);
        Assert.Equal(12000m, inDb.StandardPrice);
        Assert.Equal("Hoàng kỳ chích mật", inDb.CommonName);
    }

    #endregion

    #region UT-TU-05: Test tồn kho theo lô (FEFO/FIFO)

    [Fact]
    public async Task GetHerbBatchesAsync_NonExistingHerbId_ReturnsNull()
    {
        // Arrange
        using var context = CreateInMemoryDbContext();
        var service = new HerbService(context);

        // Act
        var result = await service.GetHerbBatchesAsync(99999);

        // Assert: Trả về null → Controller trả 404
        Assert.Null(result);
    }

    [Fact]
    public async Task GetHerbBatchesAsync_ReturnsBatchesSortedByFEFO()
    {
        // Arrange: 1 vị thuốc có 3 lô với hạn sử dụng khác nhau
        using var context = CreateInMemoryDbContext();

        var herb = new Herb
        {
            HerbId = 1,
            HerbCode = "VT001",
            CommonName = "Cam thảo",
            Unit = "g",
            StandardPrice = 5000m,
            MinThreshold = 100m,
            IsActive = true
        };
        context.Herbs.Add(herb);

        var warehouse = new Warehouse
        {
            WarehouseId = 1,
            WarehouseCode = "KHO01",
            WarehouseName = "Kho Đông y",
            IsActive = true
        };
        context.Warehouses.Add(warehouse);

        context.Batches.AddRange(
            new Batch
            {
                BatchId = 1,
                BatchNumber = "LO2024-C",
                HerbId = 1,
                WarehouseId = 1,
                ImportDate = new DateTime(2024, 6, 1),
                ExpiryDate = new DateOnly(2026, 12, 31), // Hết hạn muộn nhất
                OriginalQuantity = 500m,
                CurrentQuantity = 200m,
                UnitCost = 4800m,
                Status = "Active"
            },
            new Batch
            {
                BatchId = 2,
                BatchNumber = "LO2024-A",
                HerbId = 1,
                WarehouseId = 1,
                ImportDate = new DateTime(2024, 1, 15),
                ExpiryDate = new DateOnly(2025, 6, 30), // Hết hạn sớm nhất → FEFO đứng đầu
                OriginalQuantity = 300m,
                CurrentQuantity = 80m,
                UnitCost = 4500m,
                Status = "Active"
            },
            new Batch
            {
                BatchId = 3,
                BatchNumber = "LO2024-B",
                HerbId = 1,
                WarehouseId = 1,
                ImportDate = new DateTime(2024, 3, 10),
                ExpiryDate = new DateOnly(2026, 3, 31), // Hết hạn giữa
                OriginalQuantity = 400m,
                CurrentQuantity = 150m,
                UnitCost = 4700m,
                Status = "Active"
            }
        );
        await context.SaveChangesAsync();

        var service = new HerbService(context);

        // Act
        var result = await service.GetHerbBatchesAsync(herb.HerbId);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(herb.HerbId, result.HerbId);
        Assert.Equal(3, result.Batches.Count);

        // FEFO: LO2024-A (2025-06-30) → LO2024-B (2026-03-31) → LO2024-C (2026-12-31)
        Assert.Equal("LO2024-A", result.Batches[0].BatchNumber);
        Assert.Equal("LO2024-B", result.Batches[1].BatchNumber);
        Assert.Equal("LO2024-C", result.Batches[2].BatchNumber);
    }

    [Fact]
    public async Task GetHerbBatchesAsync_TotalCurrentQuantity_IsSumOfAllBatches()
    {
        // Arrange
        using var context = CreateInMemoryDbContext();

        var herb = new Herb
        {
            HerbId = 1,
            HerbCode = "VT001",
            CommonName = "Đương quy",
            Unit = "g",
            StandardPrice = 15000m,
            MinThreshold = 50m,
            IsActive = true
        };
        context.Herbs.Add(herb);

        var warehouse = new Warehouse
        {
            WarehouseId = 1,
            WarehouseCode = "KHO01",
            WarehouseName = "Kho Đông y",
            IsActive = true
        };
        context.Warehouses.Add(warehouse);

        context.Batches.AddRange(
            new Batch { BatchId = 1, BatchNumber = "LO-001", HerbId = 1, WarehouseId = 1, ImportDate = DateTime.UtcNow, ExpiryDate = new DateOnly(2026, 1, 1), OriginalQuantity = 500m, CurrentQuantity = 300m, UnitCost = 14000m, Status = "Active" },
            new Batch { BatchId = 2, BatchNumber = "LO-002", HerbId = 1, WarehouseId = 1, ImportDate = DateTime.UtcNow, ExpiryDate = new DateOnly(2026, 6, 1), OriginalQuantity = 500m, CurrentQuantity = 150m, UnitCost = 14500m, Status = "Active" },
            new Batch { BatchId = 3, BatchNumber = "LO-003", HerbId = 1, WarehouseId = 1, ImportDate = DateTime.UtcNow, ExpiryDate = new DateOnly(2027, 1, 1), OriginalQuantity = 200m, CurrentQuantity = 50m, UnitCost = 15000m, Status = "Active" }
        );
        await context.SaveChangesAsync();

        var service = new HerbService(context);

        // Act
        var result = await service.GetHerbBatchesAsync(herb.HerbId);

        // Assert: TotalCurrentQuantity = 300 + 150 + 50 = 500
        Assert.NotNull(result);
        Assert.Equal(500m, result.TotalCurrentQuantity);
    }

    #endregion
}

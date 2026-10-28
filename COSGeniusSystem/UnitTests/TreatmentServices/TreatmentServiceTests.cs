using COSGeniusSystem.Data;
using COSGeniusSystem.DTOs.TreatmentServices;
using COSGeniusSystem.Models;
using COSGeniusSystem.Services.TreatmentServices;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace COSGeniusSystem.UnitTests.TreatmentServices;

public class TreatmentServiceTests
{
    private static ApplicationDbContext CreateInMemoryDbContext()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(databaseName: $"ClinicHerbDb_Service_{Guid.NewGuid()}")
            .Options;

        return new ApplicationDbContext(options);
    }

    #region UT-PHUC-03: Test danh mục bảng giá dịch vụ

    [Fact]
    public async Task GetActiveServicesAsync_ReturnsOnlyActiveServicesWithValidPrice()
    {
        // Arrange: Seed dữ liệu gồm cả IsActive = true và IsActive = false
        using var context = CreateInMemoryDbContext();
        context.Services.AddRange(
            new Service
            {
                ServiceId = 1,
                ServiceCode = "DV001",
                ServiceName = "Châm cứu",
                Price = 150000m,
                Unit = "Lần",
                IsActive = true
            },
            new Service
            {
                ServiceId = 2,
                ServiceCode = "DV002",
                ServiceName = "Xoa bóp bấm huyệt",
                Price = 200000m,
                Unit = "Liệu trình",
                IsActive = true
            },
            new Service
            {
                ServiceId = 3,
                ServiceCode = "DV003",
                ServiceName = "Dịch vụ đã dừng cung cấp",
                Price = 50000m,
                Unit = "Lần",
                IsActive = false
            }
        );
        await context.SaveChangesAsync();

        var service = new TreatmentService(context);

        // Act: Lấy danh sách dịch vụ niêm yết
        var result = await service.GetActiveServicesAsync();

        // Assert: Chỉ trả về các dịch vụ đang khả dụng (IsActive == true)
        Assert.NotNull(result);
        Assert.Equal(2, result.Count);
        Assert.All(result, s =>
        {
            Assert.True(s.IsActive);
            Assert.True(s.Price > 0, "Price phải lớn hơn 0");
            Assert.IsType<decimal>(s.Price);
        });
        Assert.DoesNotContain(result, s => s.ServiceId == 3);
    }

    #endregion

    #region UT-PHUC-04: Test thêm mới dịch vụ trị liệu

    [Theory]
    [InlineData(0)]
    [InlineData(-1000)]
    public async Task CreateServiceAsync_PriceLessOrEqualToZero_ReturnsValidationError(decimal invalidPrice)
    {
        // Arrange: Truyền CreateTreatmentServiceRequest có Price = 0 hoặc Price = -1000
        using var context = CreateInMemoryDbContext();
        var service = new TreatmentService(context);

        var request = new CreateTreatmentServiceRequest
        {
            ServiceCode = "DV_TEST",
            ServiceName = "Thủy châm",
            Price = invalidPrice,
            Unit = "Lần"
        };

        // Act
        var (dto, errorMessage) = await service.CreateServiceAsync(request);

        // Assert: Thất bại do lỗi validation giá
        Assert.Null(dto);
        Assert.NotNull(errorMessage);
        Assert.Contains("Price must be greater than 0", errorMessage);

        // Đảm bảo không bản ghi nào được thêm vào DB
        Assert.Empty(await context.Services.ToListAsync());
    }

    [Fact]
    public async Task CreateServiceAsync_DuplicateServiceNameOrCode_ReturnsConflictError()
    {
        // Arrange: Seed sẵn một dịch vụ có tên "Châm cứu"
        using var context = CreateInMemoryDbContext();
        context.Services.Add(new Service
        {
            ServiceId = 1,
            ServiceCode = "DV001",
            ServiceName = "Châm cứu",
            Price = 150000m,
            Unit = "Lần",
            IsActive = true
        });
        await context.SaveChangesAsync();

        var service = new TreatmentService(context);

        var requestDuplicateName = new CreateTreatmentServiceRequest
        {
            ServiceCode = "DV999",
            ServiceName = "Châm cứu", // Trùng tên
            Price = 200000m,
            Unit = "Lần"
        };

        // Act
        var (dto, errorMessage) = await service.CreateServiceAsync(requestDuplicateName);

        // Assert: Báo lỗi trùng lặp tên dịch vụ (Conflict / Bad Request)
        Assert.Null(dto);
        Assert.NotNull(errorMessage);
        Assert.Contains("already exists", errorMessage);
    }

    [Fact]
    public async Task CreateServiceAsync_ValidData_CreatesSuccessfullyWithActiveStatus()
    {
        // Arrange: Dữ liệu hợp lệ (Price > 0, tên và mã không trùng)
        using var context = CreateInMemoryDbContext();
        var service = new TreatmentService(context);

        var request = new CreateTreatmentServiceRequest
        {
            ServiceCode = "DV010",
            ServiceName = "Cấy chỉ trị liệu",
            Price = 350000m,
            Unit = "Lần",
            Description = "Cấy chỉ catgut vào huyệt vị"
        };

        // Act
        var (dto, errorMessage) = await service.CreateServiceAsync(request);

        // Assert: Tạo thành công
        Assert.Null(errorMessage);
        Assert.NotNull(dto);
        Assert.Equal("DV010", dto.ServiceCode);
        Assert.Equal("Cấy chỉ trị liệu", dto.ServiceName);
        Assert.Equal(350000m, dto.Price);
        Assert.True(dto.IsActive);

        // Kiểm tra lưu đúng trong Database
        var savedEntity = await context.Services.FirstOrDefaultAsync(s => s.ServiceCode == "DV010");
        Assert.NotNull(savedEntity);
        Assert.True(savedEntity.IsActive);
        Assert.Equal(350000m, savedEntity.Price);
    }

    #endregion

    #region UT-PHUC-05: Test cập nhật dịch vụ trị liệu

    [Fact]
    public async Task UpdateServiceAsync_ValidRequest_UpdatesServicePriceSuccessfully()
    {
        // Arrange: Dịch vụ hiện có với giá ban đầu 100k
        using var context = CreateInMemoryDbContext();
        var existing = new Service
        {
            ServiceId = 1,
            ServiceCode = "DV001",
            ServiceName = "Bấm huyệt",
            Price = 100000m,
            Unit = "Lần",
            IsActive = true
        };
        context.Services.Add(existing);
        await context.SaveChangesAsync();

        var service = new TreatmentService(context);

        var updateRequest = new UpdateTreatmentServiceRequest
        {
            ServiceName = "Bấm huyệt chuyên sâu",
            Price = 150000m, // Giá mới
            Unit = "Lần",
            Description = "Nâng cấp kỹ thuật bấm huyệt",
            IsActive = true
        };

        // Act
        var (dto, errorMessage) = await service.UpdateServiceAsync(existing.ServiceId, updateRequest);

        // Assert: Cập nhật thành công và DB đổi giá
        Assert.Null(errorMessage);
        Assert.NotNull(dto);
        Assert.Equal(150000m, dto.Price);
        Assert.Equal("Bấm huyệt chuyên sâu", dto.ServiceName);

        var inDb = await context.Services.FindAsync(existing.ServiceId);
        Assert.NotNull(inDb);
        Assert.Equal(150000m, inDb.Price);
        Assert.Equal("Bấm huyệt chuyên sâu", inDb.ServiceName);
    }

    [Fact]
    public async Task UpdateServiceAsync_PriceChange_PreservesHistoricInvoiceDetailUnitPrice()
    {
        // Arrange:
        // 1. Seed dịch vụ giá 100k
        using var context = CreateInMemoryDbContext();
        var treatmentService = new Service
        {
            ServiceId = 1,
            ServiceCode = "DV001",
            ServiceName = "Xoa bóp vai gáy",
            Price = 100000m,
            Unit = "Lần",
            IsActive = true
        };
        context.Services.Add(treatmentService);

        // 2. Seed 1 hóa đơn và chi tiết hóa đơn cũ đã lập với đơn giá 100k
        var historicInvoiceDetail = new InvoiceDetail
        {
            InvoiceDetailId = 1,
            InvoiceId = 100,
            ItemType = "Service",
            ReferenceId = treatmentService.ServiceId,
            ItemName = treatmentService.ServiceName,
            Quantity = 1,
            UnitPrice = 100000m, // Đơn giá tại thời điểm lập hóa đơn cũ
            SubTotal = 100000m
        };
        context.InvoiceDetails.Add(historicInvoiceDetail);
        await context.SaveChangesAsync();

        var service = new TreatmentService(context);

        // Act: Thực hiện cập nhật giá dịch vụ lên 150k
        var updateRequest = new UpdateTreatmentServiceRequest
        {
            ServiceName = "Xoa bóp vai gáy (Áp dụng giá mới)",
            Price = 150000m,
            Unit = "Lần",
            IsActive = true
        };
        var (dto, errorMessage) = await service.UpdateServiceAsync(treatmentService.ServiceId, updateRequest);

        // Assert:
        // 1. Giá dịch vụ mới trong bảng Service đã đổi lên 150k
        Assert.Null(errorMessage);
        Assert.NotNull(dto);
        Assert.Equal(150000m, dto.Price);

        // 2. Tính bất biến của hóa đơn cũ: Đơn giá trong InvoiceDetail cũ VẪN GIỮ NGUYÊN 100k
        var oldDetailInDb = await context.InvoiceDetails.FindAsync(historicInvoiceDetail.InvoiceDetailId);
        Assert.NotNull(oldDetailInDb);
        Assert.Equal(100000m, oldDetailInDb.UnitPrice);
        Assert.NotEqual(150000m, oldDetailInDb.UnitPrice);
    }

    #endregion
}

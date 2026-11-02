using COSGeniusSystem.Data;
using COSGeniusSystem.DTOs;
using COSGeniusSystem.Models;
using COSGeniusSystem.Services;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace COSGeniusSystem.UnitTests;

public class TreatmentServiceManagerTests
{
    private static ApplicationDbContext CreateInMemoryDbContext()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(databaseName: $"ClinicHerbDb_ServiceMgr_{Guid.NewGuid()}")
            .Options;

        return new ApplicationDbContext(options);
    }

    [Fact]
    public async Task GetServicesAsync_KeywordSearch_ReturnsMatchingServices()
    {
        using var context = CreateInMemoryDbContext();
        context.Services.AddRange(
            new Service { ServiceId = 10, ServiceCode = "DV00010", ServiceName = "Châm cứu cơ bản", Price = 100000m, Unit = "Buổi", IsActive = true },
            new Service { ServiceId = 11, ServiceCode = "DV00011", ServiceName = "Xoa bóp bấm huyệt", Price = 120000m, Unit = "Buổi", IsActive = true }
        );
        await context.SaveChangesAsync();

        var service = new TreatmentService(context);
        var result = await service.GetServicesAsync(new TreatmentServiceFilterQuery { Keyword = "Châm cứu" });

        Assert.Single(result);
        Assert.Equal("DV00010", result[0].ServiceCode);
    }

    [Fact]
    public async Task GetServiceByIdAsync_ValidId_ReturnsServiceDto()
    {
        using var context = CreateInMemoryDbContext();
        context.Services.Add(new Service
        {
            ServiceId = 20,
            ServiceCode = "DV00020",
            ServiceName = "Giác hơi trị liệu",
            Price = 90000m,
            Unit = "Buổi",
            Description = "Giác hơi bài độc",
            IsActive = true
        });
        await context.SaveChangesAsync();

        var service = new TreatmentService(context);
        var result = await service.GetServiceByIdAsync(20);

        Assert.NotNull(result);
        Assert.Equal("DV00020", result.ServiceCode);
        Assert.Equal(90000m, result.Price);
    }

    [Fact]
    public async Task DeleteServiceAsync_WithoutPlans_HardDeletesService()
    {
        using var context = CreateInMemoryDbContext();
        context.Services.Add(new Service
        {
            ServiceId = 30,
            ServiceCode = "DV00030",
            ServiceName = "Dịch vụ thử nghiệm",
            Price = 50000m,
            Unit = "Buổi",
            IsActive = true
        });
        await context.SaveChangesAsync();

        var service = new TreatmentService(context);
        var (success, isDeactivated, error) = await service.DeleteServiceAsync(30);

        Assert.True(success);
        Assert.False(isDeactivated);
        Assert.Null(error);

        var exists = await context.Services.AnyAsync(s => s.ServiceId == 30);
        Assert.False(exists);
    }

    [Fact]
    public async Task DeleteServiceAsync_WithPlans_SoftDeactivatesService()
    {
        using var context = CreateInMemoryDbContext();
        context.Services.Add(new Service
        {
            ServiceId = 40,
            ServiceCode = "DV00040",
            ServiceName = "Dịch vụ đã có phác đồ",
            Price = 150000m,
            Unit = "Buổi",
            IsActive = true
        });
        context.TreatmentPlanDetails.Add(new TreatmentPlanDetail
        {
            PlanDetailId = 1,
            PlanId = 1,
            ServiceId = 40,
            ServiceNameSnapshot = "Dịch vụ đã có phác đồ",
            UnitSnapshot = "Buổi",
            PlannedQuantity = 5,
            UnitPrice = 150000m
        });
        await context.SaveChangesAsync();

        var service = new TreatmentService(context);
        var (success, isDeactivated, error) = await service.DeleteServiceAsync(40);

        Assert.True(success);
        Assert.True(isDeactivated);
        Assert.Null(error);

        var saved = await context.Services.FindAsync(40);
        Assert.NotNull(saved);
        Assert.False(saved.IsActive);
    }
}

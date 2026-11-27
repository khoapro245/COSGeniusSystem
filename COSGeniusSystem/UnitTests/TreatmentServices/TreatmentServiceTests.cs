using System;
using System.Threading.Tasks;
using COSGeniusSystem.Data;
using COSGeniusSystem.DTOs;
using COSGeniusSystem.Models;
using COSGeniusSystem.Services;
using Microsoft.EntityFrameworkCore;
using Xunit;

public class TreatmentServiceTests : IDisposable
{
    private readonly ApplicationDbContext _db;
    private readonly TreatmentService _service;

    public TreatmentServiceTests()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options;
        _db = new ApplicationDbContext(options);
        _service = new TreatmentService(_db);
    }

    private async Task<Service> SeedAsync(int id = 1, string code = "DV00001",
        string name = "Acupuncture")
    {
        var entity = new Service
        {
            ServiceId = id, ServiceCode = code, ServiceName = name,
            Price = 150000, Unit = "Buổi", IsActive = true
        };
        _db.Services.Add(entity);
        await _db.SaveChangesAsync();
        return entity;
    }

    private static CreateTreatmentServiceRequest CreateRequest(string? code = "DV00001",
        string name = "Acupuncture", decimal price = 150000) => new()
    {
        ServiceCode = code, ServiceName = name, Price = price,
        Unit = "Buổi", IsActive = true
    };

    private static UpdateTreatmentServiceRequest UpdateRequest(
        string name = "Updated Acupuncture", decimal price = 200000) => new()
    {
        ServiceName = name, Price = price, Unit = "Lần", IsActive = true
    };

    // UTC-SERVICE-001
    [Fact]
    public async Task CreateServiceAsync_ValidRequest_CreatesService()
    {
        var result = await _service.CreateServiceAsync(CreateRequest());
        Assert.NotNull(result.Dto);
        Assert.Null(result.ErrorMessage);
        Assert.Equal("DV00001", result.Dto!.ServiceCode);
        Assert.Equal("Acupuncture", result.Dto.ServiceName);
        Assert.Equal(150000m, result.Dto.Price);
        Assert.Equal("Buổi", result.Dto.Unit);
        Assert.True(result.Dto.IsActive);
        _db.ChangeTracker.Clear();
        Assert.Equal("DV00001", (await _db.Services.SingleAsync()).ServiceCode);
    }

    // UTC-SERVICE-002
    [Fact]
    public async Task CreateServiceAsync_NoCode_GeneratesCode()
    {
        var result = await _service.CreateServiceAsync(CreateRequest(null, "Massage", 100000));
        Assert.NotNull(result.Dto);
        Assert.Null(result.ErrorMessage);
        Assert.Equal("DV00001", result.Dto!.ServiceCode);
        Assert.Equal("DV00001", (await _db.Services.SingleAsync()).ServiceCode);
    }

    // UTC-SERVICE-003
    [Fact]
    public async Task CreateServiceAsync_ZeroPrice_ReturnsError()
    {
        var result = await _service.CreateServiceAsync(CreateRequest(price: 0));
        Assert.Null(result.Dto);
        Assert.Equal("Price must be greater than 0.", result.ErrorMessage);
        Assert.Equal(0, await _db.Services.CountAsync());
    }

    // UTC-SERVICE-004
    [Fact]
    public async Task CreateServiceAsync_DuplicateCode_ReturnsError()
    {
        await SeedAsync();
        var result = await _service.CreateServiceAsync(CreateRequest(name: "New Service"));
        Assert.Null(result.Dto);
        Assert.Equal("Service code already exists.", result.ErrorMessage);
        Assert.Equal(1, await _db.Services.CountAsync());
    }

    // UTC-SERVICE-005
    [Fact]
    public async Task CreateServiceAsync_DuplicateName_ReturnsError()
    {
        await SeedAsync();
        var result = await _service.CreateServiceAsync(CreateRequest(code: "DV00002"));
        Assert.Null(result.Dto);
        Assert.Equal("Service name already exists.", result.ErrorMessage);
        Assert.Equal(1, await _db.Services.CountAsync());
    }

    // UTC-SERVICE-006
    [Fact]
    public async Task UpdateServiceAsync_ValidRequest_UpdatesService()
    {
        await SeedAsync();
        var result = await _service.UpdateServiceAsync(1, UpdateRequest());
        Assert.NotNull(result.Dto);
        Assert.Null(result.ErrorMessage);
        Assert.Equal("Updated Acupuncture", result.Dto!.ServiceName);
        Assert.Equal(200000m, result.Dto.Price);
        Assert.Equal("Lần", result.Dto.Unit);
        _db.ChangeTracker.Clear();
        var saved = await _db.Services.SingleAsync();
        Assert.Equal("Updated Acupuncture", saved.ServiceName);
        Assert.Equal(200000m, saved.Price);
        Assert.Equal("Lần", saved.Unit);
    }

    // UTC-SERVICE-007
    [Fact]
    public async Task UpdateServiceAsync_NotFound_ReturnsNull()
    {
        var result = await _service.UpdateServiceAsync(99, UpdateRequest("Updated Service"));
        Assert.Null(result.Dto);
        Assert.Null(result.ErrorMessage);
    }

    // UTC-SERVICE-008
    [Fact]
    public async Task UpdateServiceAsync_NegativePrice_ReturnsError()
    {
        await SeedAsync();
        var result = await _service.UpdateServiceAsync(1, UpdateRequest(price: -1000));
        Assert.Null(result.Dto);
        Assert.Equal("Price must be greater than 0.", result.ErrorMessage);
        _db.ChangeTracker.Clear();
        Assert.Equal(150000m, (await _db.Services.SingleAsync()).Price);
    }

    // UTC-SERVICE-009
    [Fact]
    public async Task UpdateServiceAsync_DuplicateName_ReturnsError()
    {
        await SeedAsync();
        await SeedAsync(2, "DV00002", "Massage");
        var result = await _service.UpdateServiceAsync(1, UpdateRequest("Massage"));
        Assert.Null(result.Dto);
        Assert.Equal("Name 'Massage' is already used by another service.", result.ErrorMessage);
        _db.ChangeTracker.Clear();
        Assert.Equal("Acupuncture", (await _db.Services.SingleAsync(s => s.ServiceId == 1)).ServiceName);
    }

    // UTC-SERVICE-010
    [Fact]
    public async Task DeleteServiceAsync_Unreferenced_DeletesService()
    {
        await SeedAsync();
        var result = await _service.DeleteServiceAsync(1);
        Assert.True(result.Success);
        Assert.False(result.IsDeactivated);
        Assert.Null(result.ErrorMessage);
        _db.ChangeTracker.Clear();
        Assert.False(await _db.Services.AnyAsync());
    }

    // UTC-SERVICE-011
    [Fact]
    public async Task DeleteServiceAsync_Referenced_DeactivatesService()
    {
        var entity = await SeedAsync();
        // A linked detail is sufficient for this service's branch.
        // Populate any additional required model fields if needed by your schema.
        entity.TreatmentPlanDetails.Add(new());
        await _db.SaveChangesAsync();
        _db.ChangeTracker.Clear();
        var result = await _service.DeleteServiceAsync(1);
        Assert.True(result.Success);
        Assert.True(result.IsDeactivated);
        Assert.Null(result.ErrorMessage);
        _db.ChangeTracker.Clear();
        var saved = await _db.Services.Include(s => s.TreatmentPlanDetails).SingleAsync();
        Assert.False(saved.IsActive);
        Assert.Single(saved.TreatmentPlanDetails);
    }

    // UTC-SERVICE-012
    [Fact]
    public async Task DeleteServiceAsync_NotFound_ReturnsError()
    {
        var result = await _service.DeleteServiceAsync(99);
        Assert.False(result.Success);
        Assert.False(result.IsDeactivated);
        Assert.Equal("Treatment service was not found.", result.ErrorMessage);
    }

    public void Dispose() => _db.Dispose();
}

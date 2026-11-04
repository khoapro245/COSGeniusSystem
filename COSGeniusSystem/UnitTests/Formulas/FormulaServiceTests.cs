using System.Security.Claims;
using COSGeniusSystem.Commons;
using COSGeniusSystem.Data;
using COSGeniusSystem.DTOs.Formulas;
using COSGeniusSystem.Exceptions;
using COSGeniusSystem.Models;
using COSGeniusSystem.Services.Formulas;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace COSGeniusSystem.UnitTests;

public class FormulaServiceTests
{
    private static ApplicationDbContext CreateInMemoryDbContext()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(databaseName: $"ClinicHerbDb_Formula_{Guid.NewGuid()}")
            .Options;

        return new ApplicationDbContext(options);
    }

    private static ClaimsPrincipal CreateManagerPrincipal(int userId = 1002)
    {
        return new ClaimsPrincipal(new ClaimsIdentity(new[]
        {
            new Claim(ClaimTypes.NameIdentifier, userId.ToString()),
            new Claim(ClaimTypes.Role, RoleConstants.Manager),
        }, "TestAuth"));
    }

    private static ClaimsPrincipal CreateDoctorPrincipal(int userId = 2)
    {
        return new ClaimsPrincipal(new ClaimsIdentity(new[]
        {
            new Claim(ClaimTypes.NameIdentifier, userId.ToString()),
            new Claim(ClaimTypes.Role, RoleConstants.Doctor),
        }, "TestAuth"));
    }

    [Fact]
    public async Task GetFormulasAsync_FilterByStatus_ReturnsMatching()
    {
        using var context = CreateInMemoryDbContext();
        var doc = new Doctor { DoctorId = 1, UserId = 2, FullName = "BS. Minh An", IsActive = true };
        context.Doctors.Add(doc);

        var f1 = new HerbalFormula { HerbalFormulaId = 1, FormulaCode = "CT1", FormulaName = "Công thức A", Status = "APPROVED", ProposedByDoctorId = 1, IsActive = true, CreatedAt = DateTime.UtcNow };
        var f2 = new HerbalFormula { HerbalFormulaId = 2, FormulaCode = "CT2", FormulaName = "Công thức B", Status = "PENDING", ProposedByDoctorId = 1, IsActive = true, CreatedAt = DateTime.UtcNow };
        context.HerbalFormulas.AddRange(f1, f2);
        await context.SaveChangesAsync();

        var service = new FormulaService(context);
        var result = await service.GetFormulasAsync(new FormulaFilterQuery { Status = "PENDING" });

        Assert.Single(result);
        Assert.Equal("Công thức B", result[0].FormulaName);
        Assert.Equal("PENDING", result[0].Status);
    }

    [Fact]
    public async Task GetFormulaByIdAsync_Existing_ReturnsFormulaWithItems()
    {
        using var context = CreateInMemoryDbContext();
        var doc = new Doctor { DoctorId = 1, UserId = 2, FullName = "BS. Minh An", IsActive = true };
        var herb = new Herb { HerbId = 1, HerbCode = "VT00001", CommonName = "Cam Thảo", Unit = "g", IsActive = true };
        context.Doctors.Add(doc);
        context.Herbs.Add(herb);

        var f1 = new HerbalFormula { HerbalFormulaId = 1, FormulaCode = "CT1", FormulaName = "Công thức Mẫu", Status = "APPROVED", ProposedByDoctorId = 1, IsActive = true, CreatedAt = DateTime.UtcNow };
        f1.HerbalFormulaDetails.Add(new HerbalFormulaDetail { HerbId = 1, DosagePerDose = 8, RoleInFormula = "Quân", Note = "Vị chính" });
        context.HerbalFormulas.Add(f1);
        await context.SaveChangesAsync();

        var service = new FormulaService(context);
        var result = await service.GetFormulaByIdAsync(1);

        Assert.NotNull(result);
        Assert.Equal("Công thức Mẫu", result.FormulaName);
        Assert.Single(result.Items);
        Assert.Equal("Cam Thảo", result.Items[0].HerbName);
        Assert.Equal(8, result.Items[0].DosagePerDose);
    }

    [Fact]
    public async Task CreateFormulaAsync_ManagerActor_CreatesApprovedFormula()
    {
        using var context = CreateInMemoryDbContext();
        var doc = new Doctor { DoctorId = 1, UserId = 2, FullName = "BS. Minh An", IsActive = true };
        var herb = new Herb { HerbId = 1, HerbCode = "VT00001", CommonName = "Cam Thảo", Unit = "g", IsActive = true };
        context.Doctors.Add(doc);
        context.Herbs.Add(herb);
        await context.SaveChangesAsync();

        var service = new FormulaService(context);
        var request = new CreateHerbalFormulaRequest
        {
            FormulaName = "Bài thuốc Bổ Khí",
            UsageInstructions = "Sắc uống ngày 2 lần",
            IntendedUse = "Bổ trung ích khí",
            Items = new List<CreateFormulaItemRequest>
            {
                new CreateFormulaItemRequest(1, 10, "Quân", "Cam thảo sao vàng")
            }
        };

        var result = await service.CreateFormulaAsync(CreateManagerPrincipal(), request);

        Assert.NotNull(result);
        Assert.Equal("Bài thuốc Bổ Khí", result.FormulaName);
        Assert.Equal("APPROVED", result.Status);
        Assert.True(result.IsActive);
        Assert.Single(result.Items);
    }

    [Fact]
    public async Task UpdateFormulaAsync_UpdatesInfoAndItems()
    {
        using var context = CreateInMemoryDbContext();
        var doc = new Doctor { DoctorId = 1, UserId = 2, FullName = "BS. Minh An", IsActive = true };
        var h1 = new Herb { HerbId = 1, HerbCode = "VT00001", CommonName = "Cam Thảo", Unit = "g", IsActive = true };
        var h2 = new Herb { HerbId = 2, HerbCode = "VT00002", CommonName = "Kỉ Tử", Unit = "g", IsActive = true };
        context.Doctors.Add(doc);
        context.Herbs.AddRange(h1, h2);

        var f = new HerbalFormula { HerbalFormulaId = 1, FormulaCode = "CT1", FormulaName = "Cũ", Status = "APPROVED", ProposedByDoctorId = 1, IsActive = true, CreatedAt = DateTime.UtcNow };
        f.HerbalFormulaDetails.Add(new HerbalFormulaDetail { HerbId = 1, DosagePerDose = 5 });
        context.HerbalFormulas.Add(f);
        await context.SaveChangesAsync();

        var service = new FormulaService(context);
        var updateReq = new UpdateHerbalFormulaRequest
        {
            FormulaName = "Công thức mới cập nhật",
            UsageInstructions = "Uống ấm",
            IntendedUse = "Mới",
            Items = new List<CreateFormulaItemRequest>
            {
                new CreateFormulaItemRequest(2, 12, "Thần", "Thêm Kỉ Tử")
            }
        };

        var updated = await service.UpdateFormulaAsync(CreateManagerPrincipal(), 1, updateReq);

        Assert.Equal("Công thức mới cập nhật", updated.FormulaName);
        Assert.Single(updated.Items);
        Assert.Equal("Kỉ Tử", updated.Items[0].HerbName);
    }

    [Fact]
    public async Task ToggleFormulaStatusAsync_TogglesIsActive()
    {
        using var context = CreateInMemoryDbContext();
        var doc = new Doctor { DoctorId = 1, UserId = 2, FullName = "BS. Minh An", IsActive = true };
        context.Doctors.Add(doc);

        var f = new HerbalFormula { HerbalFormulaId = 1, FormulaCode = "CT1", FormulaName = "Thuốc", Status = "APPROVED", ProposedByDoctorId = 1, IsActive = true, CreatedAt = DateTime.UtcNow };
        context.HerbalFormulas.Add(f);
        await context.SaveChangesAsync();

        var service = new FormulaService(context);
        var disabled = await service.ToggleFormulaStatusAsync(CreateManagerPrincipal(), 1);
        Assert.False(disabled.IsActive);

        var reenabled = await service.ToggleFormulaStatusAsync(CreateManagerPrincipal(), 1);
        Assert.True(reenabled.IsActive);
    }

    [Fact]
    public async Task ReviewFormulaAsync_ApproveAndReject_WorkCorrectly()
    {
        using var context = CreateInMemoryDbContext();
        var doc = new Doctor { DoctorId = 1, UserId = 2, FullName = "BS. Minh An", IsActive = true };
        context.Doctors.Add(doc);

        var f1 = new HerbalFormula { HerbalFormulaId = 1, FormulaCode = "CT1", FormulaName = "Đề xuất 1", Status = "PENDING", ProposedByDoctorId = 1, IsActive = true, CreatedAt = DateTime.UtcNow };
        var f2 = new HerbalFormula { HerbalFormulaId = 2, FormulaCode = "CT2", FormulaName = "Đề xuất 2", Status = "PENDING", ProposedByDoctorId = 1, IsActive = true, CreatedAt = DateTime.UtcNow };
        context.HerbalFormulas.AddRange(f1, f2);
        await context.SaveChangesAsync();

        var service = new FormulaService(context);

        var approved = await service.ReviewFormulaAsync(CreateManagerPrincipal(1002), 1, new ReviewFormulaRequest { Approved = true, ReviewNote = "Đồng ý" });
        Assert.Equal("APPROVED", approved.Status);
        Assert.True(approved.IsActive);

        var rejected = await service.ReviewFormulaAsync(CreateManagerPrincipal(1002), 2, new ReviewFormulaRequest { Approved = false, ReviewNote = "Chưa phù hợp" });
        Assert.Equal("REJECTED", rejected.Status);
    }
}


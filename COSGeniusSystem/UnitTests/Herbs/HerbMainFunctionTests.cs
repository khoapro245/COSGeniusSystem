using COSGeniusSystem.Data;
using COSGeniusSystem.DTOs;
using COSGeniusSystem.Models;
using COSGeniusSystem.Services;
using Microsoft.EntityFrameworkCore;

namespace COSGeniusSystem.UnitTests;

public sealed class HerbMainFunctionTests
{
    private static ApplicationDbContext NewDb() => new(
        new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase($"HerbMain_{Guid.NewGuid():N}").Options);

    private static CreateHerbRequest CreateRequest() => new()
    {
        HerbCode = "H001",
        CommonName = "New herb",
        ScientificName = "New scientific name",
        Unit = "g",
        TasteProperties = "Sweet",
        StandardPrice = 1000m,
        MinThreshold = 10
    };

    private static UpdateHerbRequest UpdateRequest() => new()
    {
        CommonName = "Updated herb",
        ScientificName = "Updated scientific name",
        Unit = "g",
        TasteProperties = "Bitter",
        StandardPrice = 1500m,
        MinThreshold = 20,
        IsActive = false
    };

    private static async Task<Herb> SeedHerbAsync(ApplicationDbContext db, int id = 1)
    {
        var herb = new Herb
        {
            HerbId = id,
            HerbCode = $"H{id:000}",
            CommonName = $"Herb {id}",
            ScientificName = $"Scientific {id}",
            Unit = "g",
            TasteProperties = "Sweet",
            StandardPrice = 1000m,
            LastImportPrice = 500m,
            MinThreshold = 5,
            IsActive = true
        };
        db.Herbs.Add(herb);
        await db.SaveChangesAsync();
        return herb;
    }

    private static async Task SeedBatchAsync(ApplicationDbContext db)
    {
        db.Warehouses.Add(new Warehouse
        {
            WarehouseId = 1,
            WarehouseCode = "WH001",
            WarehouseName = "Test warehouse",
            IsActive = true
        });
        db.Batches.Add(new Batch
        {
            BatchId = 1,
            HerbId = 1,
            WarehouseId = 1,
            BatchNumber = "B001",
            ImportDate = DateTime.UtcNow,
            ExpiryDate = DateOnly.FromDateTime(DateTime.Today).AddYears(1),
            OriginalQuantity = 50m,
            CurrentQuantity = 50m,
            UnitCost = 500m,
            Status = "Đạt chuẩn"
        });
        await db.SaveChangesAsync();
    }

    private static async Task<string> SavedStateAsync(ApplicationDbContext db)
    {
        // Compare persisted data after clearing tracking. This is not a SQL rollback test.
        db.ChangeTracker.Clear();
        var herbs = await db.Herbs.AsNoTracking().OrderBy(h => h.HerbId).Select(h => new
        {
            h.HerbId,
            h.HerbCode,
            h.CommonName,
            h.ScientificName,
            h.Unit,
            h.TasteProperties,
            h.StandardPrice,
            h.LastImportPrice,
            h.MinThreshold,
            h.IsActive
        }).ToListAsync();
        var batches = await db.Batches.AsNoTracking().OrderBy(b => b.BatchId).Select(b => new
        {
            b.BatchId,
            b.HerbId,
            b.OriginalQuantity,
            b.CurrentQuantity,
            b.UnitCost,
            b.Status
        }).ToListAsync();
        return System.Text.Json.JsonSerializer.Serialize(new
        {
            herbs,
            batches,
            PoLineCount = await db.Podetails.CountAsync(),
            FormulaLineCount = await db.HerbalFormulaDetails.CountAsync(),
            PrescriptionLineCount = await db.HerbalPrescriptionDetails.CountAsync()
        });
    }

    #region HerbService.CreateHerbAsync

    [Fact]
    [Trait("Module", "Herb")]
    [Trait("Method", "CreateHerbAsync")]
    [Trait("TestCaseId", "UTC-HERB-MAIN-001")]
    public async Task CreateHerbAsync_ValidRequest_CreatesActiveHerbAndReturnsDto()
    {
        using var db = NewDb();
        var (dto, error) = await new HerbService(db).CreateHerbAsync(CreateRequest());
        Assert.NotNull(dto);
        Assert.Null(error);
        Assert.Equal("H001", dto!.HerbCode);
        Assert.Equal("New herb", dto.CommonName);
        Assert.Equal(1000m, dto.StandardPrice);
        Assert.True(dto.IsActive);

        db.ChangeTracker.Clear();
        var saved = Assert.Single(await db.Herbs.AsNoTracking().ToListAsync());
        Assert.Equal(dto.HerbId, saved.HerbId);
        Assert.Equal("New scientific name", saved.ScientificName);
        Assert.Equal("g", saved.Unit);
        Assert.Equal("Sweet", saved.TasteProperties);
        Assert.Equal(10m, Convert.ToDecimal(saved.MinThreshold));
        Assert.True(saved.IsActive);
        Assert.Empty(await db.Batches.ToListAsync());
    }

    [Fact]
    [Trait("Module", "Herb")]
    [Trait("Method", "CreateHerbAsync")]
    [Trait("TestCaseId", "UTC-HERB-MAIN-002")]
    public async Task CreateHerbAsync_NonPositivePrice_ReturnsErrorWithoutSaving()
    {
        foreach (var price in new[] { 0m, -1m })
        {
            using var db = NewDb();
            var request = CreateRequest();
            request.StandardPrice = price;
            var before = await SavedStateAsync(db);

            var (dto, error) = await new HerbService(db).CreateHerbAsync(request);

            Assert.Null(dto);
            Assert.Equal("Standard price must be greater than 0.", error);
            Assert.Equal(before, await SavedStateAsync(db));
        }
    }

    [Fact]
    [Trait("Module", "Herb")]
    [Trait("Method", "CreateHerbAsync")]
    [Trait("TestCaseId", "UTC-HERB-MAIN-003")]
    public async Task CreateHerbAsync_DuplicateCode_ReturnsErrorWithoutSaving()
    {
        using var db = NewDb();
        await SeedHerbAsync(db);
        var request = CreateRequest(); // H001 is already used; the requested name differs.
        var before = await SavedStateAsync(db);

        var (dto, error) = await new HerbService(db).CreateHerbAsync(request);

        Assert.Null(dto);
        Assert.Equal("The herb code or name already exists.", error);
        Assert.Equal(before, await SavedStateAsync(db));
    }

    [Fact]
    [Trait("Module", "Herb")]
    [Trait("Method", "CreateHerbAsync")]
    [Trait("TestCaseId", "UTC-HERB-MAIN-004")]
    public async Task CreateHerbAsync_DuplicateName_ReturnsErrorWithoutSaving()
    {
        using var db = NewDb();
        await SeedHerbAsync(db);
        var request = CreateRequest();
        request.HerbCode = "H002";
        request.CommonName = "Herb 1";
        var before = await SavedStateAsync(db);

        var (dto, error) = await new HerbService(db).CreateHerbAsync(request);

        Assert.Null(dto);
        Assert.Equal("The herb code or name already exists.", error);
        Assert.Equal(before, await SavedStateAsync(db));
    }

    #endregion

    #region HerbService.UpdateHerbAsync

    [Fact]
    [Trait("Module", "Herb")]
    [Trait("Method", "UpdateHerbAsync")]
    [Trait("TestCaseId", "UTC-HERB-MAIN-005")]
    public async Task UpdateHerbAsync_ValidRequest_UpdatesCatalogueAndPreservesStockAndImportPrice()
    {
        using var db = NewDb();
        await SeedHerbAsync(db);
        await SeedHerbAsync(db, 2);
        await SeedBatchAsync(db);

        var (dto, error) = await new HerbService(db).UpdateHerbAsync(1, UpdateRequest());

        Assert.NotNull(dto);
        Assert.Null(error);
        Assert.Equal("Updated herb", dto!.CommonName);
        Assert.Equal(1500m, dto.StandardPrice);
        Assert.False(dto.IsActive);
        db.ChangeTracker.Clear();
        var saved = await db.Herbs.AsNoTracking().SingleAsync(h => h.HerbId == 1);
        Assert.Equal("H001", saved.HerbCode);
        Assert.Equal("Updated scientific name", saved.ScientificName);
        Assert.Equal("g", saved.Unit);
        Assert.Equal("Bitter", saved.TasteProperties);
        Assert.Equal(20m, Convert.ToDecimal(saved.MinThreshold));
        Assert.Equal(500m, Convert.ToDecimal(saved.LastImportPrice));
        var batch = await db.Batches.AsNoTracking().SingleAsync();
        Assert.Equal(50m, batch.CurrentQuantity);
        Assert.Equal(50m, batch.OriginalQuantity);
        Assert.Equal(500m, batch.UnitCost);
        Assert.Equal("Herb 2", (await db.Herbs.AsNoTracking().SingleAsync(h => h.HerbId == 2)).CommonName);
    }

    [Fact]
    [Trait("Module", "Herb")]
    [Trait("Method", "UpdateHerbAsync")]
    [Trait("TestCaseId", "UTC-HERB-MAIN-006")]
    public async Task UpdateHerbAsync_MissingHerb_ReturnsNullDtoAndNullError()
    {
        using var db = NewDb();
        var before = await SavedStateAsync(db);

        var (dto, error) = await new HerbService(db).UpdateHerbAsync(999, UpdateRequest());

        Assert.Null(dto);
        Assert.Null(error);
        Assert.Equal(before, await SavedStateAsync(db));
    }

    [Fact]
    [Trait("Module", "Herb")]
    [Trait("Method", "UpdateHerbAsync")]
    [Trait("TestCaseId", "UTC-HERB-MAIN-007")]
    public async Task UpdateHerbAsync_NonPositivePrice_ReturnsErrorAndPreservesSavedHerb()
    {
        foreach (var price in new[] { 0m, -1m })
        {
            using var db = NewDb();
            await SeedHerbAsync(db);
            var request = UpdateRequest();
            request.StandardPrice = price;
            var before = await SavedStateAsync(db);

            var (dto, error) = await new HerbService(db).UpdateHerbAsync(1, request);

            Assert.Null(dto);
            Assert.Equal("Standard price must be greater than 0.", error);
            Assert.Equal(before, await SavedStateAsync(db));
        }
    }

    [Fact]
    [Trait("Module", "Herb")]
    [Trait("Method", "UpdateHerbAsync")]
    [Trait("TestCaseId", "UTC-HERB-MAIN-008")]
    public async Task UpdateHerbAsync_NameUsedByAnotherHerb_ReturnsErrorAndPreservesSavedHerb()
    {
        using var db = NewDb();
        await SeedHerbAsync(db);
        await SeedHerbAsync(db, 2);
        var request = UpdateRequest();
        request.CommonName = "Herb 2";
        var before = await SavedStateAsync(db);

        var (dto, error) = await new HerbService(db).UpdateHerbAsync(1, request);

        Assert.Null(dto);
        Assert.Equal("Name 'Herb 2' is already used by another herb.", error);
        Assert.Equal(before, await SavedStateAsync(db));
    }

    [Fact]
    [Trait("Module", "Herb")]
    [Trait("Method", "UpdateHerbAsync")]
    [Trait("TestCaseId", "UTC-HERB-MAIN-009")]
    public async Task UpdateHerbAsync_UnitChangeWithExistingBatch_ReturnsErrorAndPreservesSavedData()
    {
        using var db = NewDb();
        await SeedHerbAsync(db);
        await SeedBatchAsync(db);
        var request = UpdateRequest();
        request.Unit = "kg";
        var before = await SavedStateAsync(db);

        var (dto, error) = await new HerbService(db).UpdateHerbAsync(1, request);

        Assert.Null(dto);
        Assert.Equal("The unit cannot be changed when the herb has existing batches. A reconciled data conversion process is required.", error);
        Assert.Equal(before, await SavedStateAsync(db));
    }

    #endregion

    #region HerbService.DeleteHerbAsync

    [Fact]
    [Trait("Module", "Herb")]
    [Trait("Method", "DeleteHerbAsync")]
    [Trait("TestCaseId", "UTC-HERB-MAIN-010")]
    public async Task DeleteHerbAsync_UnreferencedHerb_PhysicallyRemovesOnlySelectedHerb_CurrentBehaviour()
    {
        using var db = NewDb();
        await SeedHerbAsync(db);
        await SeedHerbAsync(db, 2);

        var (success, error) = await new HerbService(db).DeleteHerbAsync(1);

        Assert.True(success);
        Assert.Null(error);
        db.ChangeTracker.Clear();
        Assert.False(await db.Herbs.AnyAsync(h => h.HerbId == 1));
        Assert.Equal(2, Assert.Single(await db.Herbs.AsNoTracking().ToListAsync()).HerbId);
    }

    [Fact]
    [Trait("Module", "Herb")]
    [Trait("Method", "DeleteHerbAsync")]
    [Trait("TestCaseId", "UTC-HERB-MAIN-011")]
    public async Task DeleteHerbAsync_MissingHerb_ReturnsFalseAndNullError()
    {
        using var db = NewDb();
        var before = await SavedStateAsync(db);

        var (success, error) = await new HerbService(db).DeleteHerbAsync(999);

        Assert.False(success);
        Assert.Null(error);
        Assert.Equal(before, await SavedStateAsync(db));
    }

    [Fact]
    [Trait("Module", "Herb")]
    [Trait("Method", "DeleteHerbAsync")]
    [Trait("TestCaseId", "UTC-HERB-MAIN-012")]
    public async Task DeleteHerbAsync_HerbHasBatch_DeactivatesHerbAndPreservesBatchStock()
    {
        using var db = NewDb();
        await SeedHerbAsync(db);
        await SeedBatchAsync(db);

        var (success, error) = await new HerbService(db).DeleteHerbAsync(1);

        Assert.True(success);
        Assert.Null(error);
        db.ChangeTracker.Clear();
        var saved = Assert.Single(await db.Herbs.AsNoTracking().ToListAsync());
        Assert.False(saved.IsActive);
        Assert.Equal("H001", saved.HerbCode);
        var batch = Assert.Single(await db.Batches.AsNoTracking().ToListAsync());
        Assert.Equal(1, batch.HerbId);
        Assert.Equal(50m, batch.CurrentQuantity);
        Assert.Equal(50m, batch.OriginalQuantity);
    }

    [Fact]
    [Trait("Module", "Herb")]
    [Trait("Method", "DeleteHerbAsync")]
    [Trait("TestCaseId", "UTC-HERB-MAIN-013")]
    public async Task DeleteHerbAsync_HerbHasPurchaseOrderLine_DeactivatesHerbAndPreservesPurchaseHistory()
    {
        using var db = NewDb();
        await SeedHerbAsync(db);
        db.Users.Add(new User
        {
            UserId = 1,
            Username = "manager.test",
            FullName = "Manager Test",
            PasswordHash = "test-only",
            IsActive = true
        });
        db.Suppliers.Add(new Supplier
        {
            SupplierId = 1,
            SupplierCode = "SUP001",
            SupplierName = "Supplier Test",
            IsActive = true
        });
        var po = new PurchaseOrder
        {
            Poid = 1,
            Pocode = "PO00001",
            SupplierId = 1,
            CreatedBy = 1,
            OrderDate = DateTime.UtcNow,
            Status = "Nháp",
            TotalAmount = 5000m
        };
        po.Podetails.Add(new Podetail { HerbId = 1, OrderQuantity = 10m, UnitPrice = 500m });
        db.PurchaseOrders.Add(po);
        await db.SaveChangesAsync();

        var (success, error) = await new HerbService(db).DeleteHerbAsync(1);

        Assert.True(success);
        Assert.Null(error);
        db.ChangeTracker.Clear();
        Assert.False((await db.Herbs.AsNoTracking().SingleAsync()).IsActive);
        Assert.Equal("PO00001", (await db.PurchaseOrders.AsNoTracking().SingleAsync()).Pocode);
        var line = Assert.Single(await db.Podetails.AsNoTracking().ToListAsync());
        Assert.Equal(1, line.HerbId);
        Assert.Equal(10m, line.OrderQuantity);
        Assert.Equal(500m, line.UnitPrice);
    }

    [Fact]
    [Trait("Module", "Herb")]
    [Trait("Method", "DeleteHerbAsync")]
    [Trait("TestCaseId", "UTC-HERB-MAIN-014")]
    public async Task DeleteHerbAsync_HerbHasFormulaLine_DeactivatesHerbAndPreservesFormula()
    {
        using var db = NewDb();
        var herb = await SeedHerbAsync(db);
        var formula = new HerbalFormula
        {
            HerbalFormulaId = 1,
            FormulaCode = "F001",
            FormulaName = "Formula Test",
            Status = "APPROVED",
            IsActive = true,
            RowVersion = new byte[8],
            UsageInstructions = "Use as directed"
        };
        formula.HerbalFormulaDetails.Add(new HerbalFormulaDetail { Herb = herb, DosagePerDose = 10m });
        db.HerbalFormulas.Add(formula);
        await db.SaveChangesAsync();

        var (success, error) = await new HerbService(db).DeleteHerbAsync(1);

        Assert.True(success);
        Assert.Null(error);
        db.ChangeTracker.Clear();
        Assert.False((await db.Herbs.AsNoTracking().SingleAsync()).IsActive);
        Assert.Equal("Formula Test", (await db.HerbalFormulas.AsNoTracking().SingleAsync()).FormulaName);
        var line = Assert.Single(await db.HerbalFormulaDetails.AsNoTracking().ToListAsync());
        Assert.Equal(1, line.HerbId);
        Assert.Equal(10m, line.DosagePerDose);
    }

    #endregion
}

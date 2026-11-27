using System;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using COSGeniusSystem.Commons;
using COSGeniusSystem.Data;
using COSGeniusSystem.DTOs.Formulas;
using COSGeniusSystem.Exceptions;
using COSGeniusSystem.Models;
using COSGeniusSystem.Services.Formulas;
using Microsoft.EntityFrameworkCore;
using Xunit;

public class FormulaServiceTests : IDisposable
{
    private readonly ApplicationDbContext _db;
    private readonly FormulaService _service;

    public FormulaServiceTests()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options;
        _db = new ApplicationDbContext(options);
        _service = new FormulaService(_db);
    }

    private static ClaimsPrincipal Actor(bool manager = false) => new(
        new ClaimsIdentity(new[] {
            new Claim(ClaimTypes.NameIdentifier, manager ? "2" : "1"),
            new Claim(ClaimTypes.Role, manager ? RoleConstants.Manager : "DOCTOR")
        }, "Test"));

    private async Task SeedAsync()
    {
        _db.Users.AddRange(
            new User { UserId = 1, Username = "doctor01", FullName = "Test Doctor", PasswordHash = "test_hash", IsActive = true, CreatedAt = DateTime.UtcNow },
            new User { UserId = 2, Username = "manager01", FullName = "Test Manager", PasswordHash = "test_hash", IsActive = true, CreatedAt = DateTime.UtcNow });
        _db.Doctors.Add(new Doctor { DoctorId = 1, UserId = 1, FullName = "Test Doctor", Specialization = "Traditional medicine", LicenseNumber = "LIC001", IsActive = true });
        _db.Herbs.Add(new Herb { HerbId = 1, HerbCode = "VT001", CommonName = "Test herb", Unit = "g", IsActive = true });
        await _db.SaveChangesAsync();
    }

    private async Task SeedFormulaAsync(string status = "APPROVED", bool active = true)
    {
        await SeedAsync();
        var formula = new HerbalFormula
        {
            HerbalFormulaId = 1, FormulaCode = "CT00001", FormulaName = "Original Formula",
            Status = status, IsActive = active, ProposedByDoctorId = 1,
            CreatedAt = DateTime.UtcNow, ReviewNote = "[VERSION:v1]", RowVersion = new byte[8]
        };
        formula.HerbalFormulaDetails.Add(new HerbalFormulaDetail { HerbId = 1, DosagePerDose = 10 });
        _db.HerbalFormulas.Add(formula);
        await _db.SaveChangesAsync();
    }

    private static CreateHerbalFormulaRequest CreateRequest(string name = "Test Formula", decimal dose = 10, string? code = null) => new()
    {
        FormulaName = name, FormulaCode = code,
        Items = [new CreateFormulaItemRequest(1, dose, null, null)]
    };

    // UTC-FORMULA-001
    [Fact]
    public async Task CreateFormulaAsync_Doctor_CreatesPendingProposal()
    {
        await SeedAsync();
        var result = await _service.CreateFormulaAsync(Actor(), CreateRequest());
        Assert.NotNull(result);
        var formula = await _db.HerbalFormulas.Include(f => f.HerbalFormulaDetails).SingleAsync();
        Assert.Equal("PENDING", formula.Status);
        Assert.True(formula.IsActive);
        Assert.NotNull(formula.SubmittedAt);
        Assert.Equal(10m, Assert.Single(formula.HerbalFormulaDetails).DosagePerDose);
    }

    // UTC-FORMULA-002
    [Fact]
    public async Task CreateFormulaAsync_Manager_CreatesApprovedFormula()
    {
        await SeedAsync();
        var result = await _service.CreateFormulaAsync(Actor(true), CreateRequest());
        Assert.NotNull(result);
        var formula = await _db.HerbalFormulas.SingleAsync();
        Assert.Equal("APPROVED", formula.Status);
        Assert.True(formula.IsActive);
        Assert.Equal(2, formula.ReviewedBy);
        Assert.NotNull(formula.ReviewedAt);
    }

    // UTC-FORMULA-003
    [Fact]
    public async Task CreateFormulaAsync_EmptyName_ThrowsBadRequest()
    {
        await SeedAsync();
        var ex = await Assert.ThrowsAsync<BadRequestException>(() => _service.CreateFormulaAsync(Actor(), CreateRequest(name: "")));
        Assert.Equal("Formula name is required and must not exceed 150 characters.", ex.Message);
        Assert.False(await _db.HerbalFormulas.AnyAsync());
    }

    // UTC-FORMULA-004
    [Fact]
    public async Task CreateFormulaAsync_ZeroDosage_ThrowsBadRequest()
    {
        await SeedAsync();
        var ex = await Assert.ThrowsAsync<BadRequestException>(() => _service.CreateFormulaAsync(Actor(), CreateRequest(dose: 0)));
        Assert.Equal("At least one herb is required, with no duplicates, positive doses, and valid notes.", ex.Message);
        Assert.False(await _db.HerbalFormulas.AnyAsync());
    }

    // UTC-FORMULA-005
    [Fact]
    public async Task CreateFormulaAsync_DuplicateCode_ThrowsConflict()
    {
        await SeedFormulaAsync();
        var ex = await Assert.ThrowsAsync<ConflictException>(() => _service.CreateFormulaAsync(Actor(), CreateRequest(code: "CT00001")));
        Assert.Equal("Formula code 'CT00001' already exists.", ex.Message);
        Assert.Equal(1, await _db.HerbalFormulas.CountAsync());
    }

    // UTC-FORMULA-006
    [Fact]
    public async Task UpdateFormulaAsync_ValidRequest_UpdatesFormula()
    {
        await SeedFormulaAsync();
        var result = await _service.UpdateFormulaAsync(Actor(true), 1, new UpdateHerbalFormulaRequest { FormulaName = "Updated Formula", Version = "v2" });
        Assert.NotNull(result);
        _db.ChangeTracker.Clear();
        var formula = await _db.HerbalFormulas.SingleAsync();
        Assert.Equal("Updated Formula", formula.FormulaName);
        Assert.StartsWith("[VERSION:v2]", formula.ReviewNote);
    }

    // UTC-FORMULA-007
    [Fact]
    public async Task UpdateFormulaAsync_NotFound_ThrowsNotFound()
    {
        var ex = await Assert.ThrowsAsync<NotFoundException>(() => _service.UpdateFormulaAsync(Actor(true), 99, new UpdateHerbalFormulaRequest { FormulaName = "Updated Formula" }));
        Assert.Equal("Formula 99 was not found.", ex.Message);
    }

    // UTC-FORMULA-008
    [Fact]
    public async Task UpdateFormulaAsync_InvalidHerb_ThrowsBadRequest()
    {
        await SeedFormulaAsync();
        var ex = await Assert.ThrowsAsync<BadRequestException>(() => _service.UpdateFormulaAsync(Actor(true), 1, new UpdateHerbalFormulaRequest { FormulaName = "Updated Formula", Items = [new CreateFormulaItemRequest(99, 10m, null, null)] }));
        Assert.Equal("Herb 99 was not found.", ex.Message);
        _db.ChangeTracker.Clear();
        Assert.Equal("Original Formula", (await _db.HerbalFormulas.SingleAsync()).FormulaName);
    }

    // UTC-FORMULA-009
    [Fact]
    public async Task ToggleFormulaStatusAsync_ActiveFormula_Deactivates()
    {
        await SeedFormulaAsync();
        await _service.ToggleFormulaStatusAsync(Actor(true), 1);
        _db.ChangeTracker.Clear();
        Assert.False((await _db.HerbalFormulas.SingleAsync()).IsActive);
    }

    // UTC-FORMULA-010
    [Fact]
    public async Task ToggleFormulaStatusAsync_InactiveFormula_Activates()
    {
        await SeedFormulaAsync(active: false);
        await _service.ToggleFormulaStatusAsync(Actor(true), 1);
        _db.ChangeTracker.Clear();
        Assert.True((await _db.HerbalFormulas.SingleAsync()).IsActive);
    }

    // UTC-FORMULA-011
    [Fact]
    public async Task ToggleFormulaStatusAsync_NotFound_ThrowsNotFound()
    {
        var ex = await Assert.ThrowsAsync<NotFoundException>(() => _service.ToggleFormulaStatusAsync(Actor(true), 99));
        Assert.Equal("Formula 99 was not found.", ex.Message);
    }

    // UTC-FORMULA-012
    [Fact]
    public async Task ReviewFormulaAsync_Approve_UpdatesStatus()
    {
        await SeedFormulaAsync("PENDING");
        await _service.ReviewFormulaAsync(Actor(true), 1, new ReviewFormulaRequest { Approved = true, ReviewNote = "Approved" });
        _db.ChangeTracker.Clear();
        var formula = await _db.HerbalFormulas.SingleAsync();
        Assert.Equal("APPROVED", formula.Status);
        Assert.True(formula.IsActive);
        Assert.Equal(2, formula.ReviewedBy);
        Assert.NotNull(formula.ReviewedAt);
        Assert.Contains("Approved", formula.ReviewNote);
    }

    // UTC-FORMULA-013
    [Fact]
    public async Task ReviewFormulaAsync_Reject_UpdatesStatus()
    {
        await SeedFormulaAsync("PENDING");
        await _service.ReviewFormulaAsync(Actor(true), 1, new ReviewFormulaRequest { Approved = false, ReviewNote = "Invalid dosage" });
        _db.ChangeTracker.Clear();
        var formula = await _db.HerbalFormulas.SingleAsync();
        Assert.Equal("REJECTED", formula.Status);
        Assert.Contains("Invalid dosage", formula.ReviewNote);
    }

    // UTC-FORMULA-014
    [Fact]
    public async Task ReviewFormulaAsync_AlreadyApproved_ThrowsBadRequest()
    {
        await SeedFormulaAsync();
        var ex = await Assert.ThrowsAsync<BadRequestException>(() => _service.ReviewFormulaAsync(Actor(true), 1, new ReviewFormulaRequest { Approved = true, ReviewNote = "Approved again" }));
        Assert.Equal("The formula proposal has status 'APPROVED' and cannot be approved again.", ex.Message);
    }

    // UTC-FORMULA-015
    [Fact]
    public async Task ReviewFormulaAsync_NotFound_ThrowsNotFound()
    {
        var ex = await Assert.ThrowsAsync<NotFoundException>(() => _service.ReviewFormulaAsync(Actor(true), 99, new ReviewFormulaRequest { Approved = true }));
        Assert.Equal("Formula proposal 99 was not found.", ex.Message);
    }

    public void Dispose() => _db.Dispose();
}

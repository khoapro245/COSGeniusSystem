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
public class DoctorFormulaProposalTests
{
    private static ApplicationDbContext Db() => new(new DbContextOptionsBuilder<ApplicationDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
    private static ClaimsPrincipal Actor(int id) => new(new ClaimsIdentity(new[] { new Claim(ClaimTypes.NameIdentifier, id.ToString()), new Claim(ClaimTypes.Role, RoleConstants.Doctor) }, "test"));
    private static void Seed(ApplicationDbContext db)
    {
        db.Doctors.Add(new Doctor { DoctorId = 1, UserId = 10, FullName = "Doctor", IsActive = true });
        db.Herbs.Add(new Herb { HerbId = 1, HerbCode = "H1", CommonName = "Herb", Unit = "g", IsActive = true });
        db.HerbalFormulas.Add(new HerbalFormula { HerbalFormulaId = 1, FormulaCode = "CT1", FormulaName = "Base", Status = "APPROVED", IsActive = true, ProposedByDoctorId = 1 });
        db.SaveChanges();
    }
    [Fact]
    public async Task DoctorWithoutProfileCannotImpersonateAnotherDoctor()
    {
        using var db = Db(); Seed(db);
        await Assert.ThrowsAsync<BadRequestException>(() => new FormulaService(db).CreateFormulaAsync(Actor(99), new() { FormulaName = "New", Items = new() { new(1, 1, null, null) } }));
        Assert.Single(db.HerbalFormulas);
    }
    [Fact]
    public async Task SubmittedUpdateRetainsBaseAndVersionAndIsListedAfterReview()
    {
        using var db = Db(); Seed(db); var service = new FormulaService(db);
        var result = await service.CreateFormulaAsync(Actor(10), new() { FormulaName = "Update", ProposalType = "UPDATE", BaseFormulaCode = "CT1", Version = "v2", ProposalReason = "Điều chỉnh liều [UPDATE:không phải mã:v9]", Items = new() { new(1, 2, null, null) } });
        Assert.Equal("PENDING", result.Status); Assert.Equal(1, result.ProposedByDoctorId); Assert.Equal("CT1", result.BaseFormulaCode); Assert.Equal("v2", result.Version);
        Assert.Equal("Điều chỉnh liều [UPDATE:không phải mã:v9]", result.ProposalReason);
        Assert.Equal("", result.ReviewNote);
        await service.ReviewFormulaAsync(Actor(10), result.HerbalFormulaId, new() { Approved = false, ReviewNote = "Cần bổ sung" });
        var reviewed = await service.GetFormulaByIdAsync(result.HerbalFormulaId);
        Assert.Equal(result.ProposalReason, reviewed.ProposalReason);
        Assert.Contains("Cần bổ sung", reviewed.ReviewNote);
        Assert.DoesNotContain("REASON:", reviewed.ReviewNote);
        var proposals = await service.GetFormulasAsync(new() { ProposalsOnly = true });
        Assert.Single(proposals); Assert.Equal("REJECTED", proposals[0].Status);
        Assert.Equal("CT1", (await service.GetFormulaByIdAsync(1)).FormulaCode);
    }
    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public async Task NonpositiveDosageIsRejected(int dosage)
    {
        using var db = Db(); Seed(db);
        await Assert.ThrowsAsync<BadRequestException>(() => new FormulaService(db).CreateFormulaAsync(Actor(10), new() { FormulaName = "New", Items = new() { new(1, dosage, null, null) } }));
        Assert.Single(db.HerbalFormulas);
    }
    [Fact]
    public async Task UnknownBaseCannotBeSubmitted()
    {
        using var db = Db(); Seed(db);
        await Assert.ThrowsAsync<BadRequestException>(() => new FormulaService(db).CreateFormulaAsync(Actor(10), new() { FormulaName = "New", ProposalType = "UPDATE", BaseFormulaCode = "CT999", Items = new() { new(1, 1, null, null) } }));
    }
}


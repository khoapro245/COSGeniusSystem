using COSGeniusSystem.Data;
using COSGeniusSystem.Models;
using Microsoft.EntityFrameworkCore;

namespace COSGeniusSystem.UnitTests;

public class ExaminationServiceTests
{
    [Fact]
    public void Model_ContainsOnlyCurrentTables_AndNoLegacyUsageColumns()
    {
        using var db = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseSqlServer("Server=localhost;Database=SchemaChecks;Integrated Security=true;TrustServerCertificate=true").Options);
        var entities = db.Model.GetEntityTypes().ToArray();
        Assert.Equal(39, entities.Length);
        Assert.DoesNotContain(entities, e => new[] { "ExamSheet", "TreatmentProtocol", "RemedyFormula", "FormulaDetail" }.Contains(e.GetTableName()));
        var usage = db.Model.FindEntityType(typeof(UsageHistory))!;
        Assert.Null(usage.FindProperty("ExamSheetId"));
        Assert.Null(usage.FindProperty("ServiceId"));
        Assert.False(usage.FindProperty(nameof(UsageHistory.PerformedBy))!.IsNullable);
        // Query translation and model validation without a live SQL connection.
        Assert.Contains("MedicalRecord", db.MedicalRecords.ToQueryString());
        Assert.Contains("TreatmentSession", db.TreatmentSessions.ToQueryString());
        Assert.DoesNotContain("ExamSheet", db.UsageHistories.ToQueryString());
    }
}
